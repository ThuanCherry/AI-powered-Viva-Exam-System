using System.Security.Claims;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AIVES.Api.Hubs;

namespace AIVES.Api.Controllers;

[ApiController, Authorize, Route("api")]
public class WorkflowController(AivesDbContext db, WorkflowService flow, VivaEngine engine, ProviderStatus providers, IHubContext<ExamHub> hub) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private IActionResult Data(object data) => Ok(new { success = true, data });

    [HttpGet("workflow/providers")]
    public IActionResult Providers() => Data(providers.View);

    [HttpGet("workflow/gemini/models"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> GeminiModels([FromServices] GeminiModelCatalog catalog, CancellationToken ct) => Data(new {
        models = await catalog.Read(ct), quota = "Check project-specific RPM/TPM/RPD/concurrent sessions in AI Studio; catalog is not quota verification",
        quotaUrl = "https://aistudio.google.com/usage?timeRange=last-28-days&tab=rate-limit" });

    [HttpGet("workflow/catalog"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Data(await db.Courses.Where(c => c.LecturerId == UserId && c.IsActive)
        .Select(c => new { c.CourseId, c.CourseCode, c.CourseName, poolSize = db.Questions.Count(q => q.CourseId == c.CourseId && q.Status == "Approved" && q.QuestionType == "Main") }).ToListAsync(ct));

    [HttpGet("courses/{id:int}/students"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Students(int id, CancellationToken ct)
    {
        WorkflowRules.Require(await db.Courses.AnyAsync(c => c.CourseId == id && c.LecturerId == UserId, ct), "Bạn không quản lý môn học này.", 403);
        var ids = await flow.Students(id, ct);
        return Data(await db.Users.Where(u => ids.Contains(u.UserId)).OrderBy(u => u.FullName).Select(u => new { u.UserId, u.FullName, u.Email }).ToListAsync(ct));
    }

    [HttpPost("Exams"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Create(ExamInput input, CancellationToken ct) => Data(new { examId = (await flow.Save(input, UserId, null, ct)).ExamId });

    [HttpPut("Exams/{id:int}"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Update(int id, ExamInput input, CancellationToken ct) => Data(new { examId = (await flow.Save(input, UserId, id, ct)).ExamId });

    [HttpPost("Exams/{id:int}/publish"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct) => Data(new { warnings = await flow.Publish(id, UserId, ct) });

    [HttpPost("Exams/{id:int}/cancel"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        // Lock exam too, following schedule->exam lock ordering used by publication.
        if (db.Database.IsMySql()) await db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE exam_id={id} FOR UPDATE").ToListAsync(ct);
        var exam = await flow.Owned(id, UserId, ct);
        WorkflowRules.Require(exam.Status is "Draft" or "Scheduled", "Chỉ hủy nháp hoặc lịch chưa bắt đầu.", 409);
        exam.Status = "Cancelled";
        foreach (var c in exam.Candidates) c.Status = "Cancelled";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await flow.Notify(id, "cancelled", ct); return Data(new { exam.Status });
    }

    [HttpGet("schedule/mine"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Schedule(CancellationToken ct)
    {
        var rows = await db.ExamCandidates.Where(c => c.StudentId == UserId && c.Status != "Cancelled" && c.ExamSession!.Status != "Draft" && c.ExamSession.Status != "Cancelled")
            .OrderBy(c => c.ScheduledTime).Select(c => new { c.ExamId, c.CandidateId, c.Status, c.ScheduledTime,
                c.ExamSession!.ExamName, c.ExamSession.TimePerStudent, CourseName = c.ExamSession.Course!.CourseName }).ToListAsync(ct);
        return Data(rows.Select(c => new { c.ExamId, c.CandidateId, c.Status, c.ExamName, c.TimePerStudent, c.CourseName,
            scheduledTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value) : null }));
    }

    [HttpPost("Exams/{id:int}/attempt/start"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        var state = await engine.Start(id, UserId, ct); await flow.Notify(id, "attempt", ct); return Data(state);
    }

    [HttpGet("Exams/{id:int}/attempt"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Resume(int id, CancellationToken ct) => Data(await engine.Resume(id, UserId, ct));

    [HttpPost("Exams/{id:int}/attempt/answer"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Answer(int id, AnswerInput input, CancellationToken ct)
    {
        var state = await engine.Submit(id, UserId, input, ct);
        await flow.Notify(id, "attempt", ct);
        await hub.Clients.Group($"student-{UserId}-exam-{id}").SendAsync("AttemptUpdated", new { serverTime = WorkflowService.Iso(flow.Now) }, ct);
        return Data(state);
    }

    [HttpPost("Exams/{id:int}/attempt/transcribe"), Authorize(Roles = "Student"), RequestSizeLimit(500000)]
    public async Task<IActionResult> Transcribe(int id, [FromForm] string turnId, [FromForm] string chunkId, IFormFile audio, CancellationToken ct)
    {
        WorkflowRules.Require(audio != null && audio.Length is >= 44 and <= 480044 && audio.ContentType is "audio/wav" or "audio/wave", "Gửi WAV dưới 480044 bytes.", 415);
        using var buffer = new MemoryStream(); await audio!.CopyToAsync(buffer, ct);
        var state = await engine.Transcribe(id, UserId, turnId, chunkId, buffer.ToArray(), ct);
        await hub.Clients.Group($"student-{UserId}-exam-{id}").SendAsync("TranscriptUpdated", new { turnId, chunkId }, ct);
        return Data(state);
    }

    [HttpGet("Exams/{id:int}/attempt/tts"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Speak(int id, [FromQuery] string turnId, CancellationToken ct) => File(await engine.Speak(id, UserId, turnId, ct), "audio/wav");
}
