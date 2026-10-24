using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using AIVES.Api.Hubs;

namespace AIVES.Api.Controllers;

[ApiController, Authorize, Route("api")]
public class WorkflowController(WorkflowService flow, VivaEngine engine, ProviderStatus providers, IHubContext<ExamHub> hub) : ApiControllerBase
{

    [HttpGet("workflow/providers")]
    public IActionResult Providers() => Data(providers.View);

    [HttpGet("workflow/gemini/models"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> GeminiModels([FromServices] GeminiModelCatalog catalog, CancellationToken ct) => Data(new {
        models = await catalog.Read(ct), quota = "Check project-specific RPM/TPM/RPD/concurrent sessions in AI Studio; catalog is not quota verification",
        quotaUrl = "https://aistudio.google.com/usage?timeRange=last-28-days&tab=rate-limit" });

    [HttpGet("workflow/catalog"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Data(await flow.CatalogAsync(UserId, ct));

    [HttpGet("courses/{id:int}/students"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Students(int id, CancellationToken ct) => Data(await flow.CourseStudentsAsync(id, UserId, ct));

    [HttpPost("Exams"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Create(ExamInput input, CancellationToken ct) => Data(new { examId = (await flow.Save(input, UserId, null, ct)).ExamId });

    [HttpPut("Exams/{id:int}"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Update(int id, ExamInput input, CancellationToken ct) => Data(new { examId = (await flow.Save(input, UserId, id, ct)).ExamId });

    [HttpPost("Exams/{id:int}/publish"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct) => Data(new { warnings = await flow.Publish(id, UserId, ct) });

    [HttpPost("Exams/{id:int}/cancel"), Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) => Data(new { status = await flow.CancelAsync(id, UserId, ct) });

    [HttpGet("schedule/mine"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Schedule(CancellationToken ct) => Data(await flow.ScheduleAsync(UserId, ct));

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
