using System.Security.Claims;
using AIVES.Api.Data;
using AIVES.Api.Features.Lecturer;
using AIVES.Api.Features.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Lecturer"), Route("api/lecturer")]
public class LecturerController(AivesDbContext db, LecturerService service, WorkflowService flow, DocumentStore documents) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private IActionResult Data(object data) => Ok(new { success = true, data });

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var courses = db.Courses.Where(c => c.LecturerId == UserId && c.IsActive);
        var classes = db.AcademicClasses.Where(c => c.LecturerId == UserId && c.Course!.LecturerId == UserId && c.IsActive);
        return Data(new { courseCount = await courses.CountAsync(ct), classCount = await classes.CountAsync(ct),
            studentCount = await db.ClassStudents.Where(s => classes.Select(c => c.ClassId).Contains(s.ClassId)).Select(s => s.StudentId).Distinct().CountAsync(ct),
            questionCount = await db.Questions.CountAsync(q => q.Course!.LecturerId == UserId && q.Status != "Archived", ct),
            documentCount = await db.Documents.CountAsync(d => d.Course!.LecturerId == UserId && d.Status != "Archived", ct),
            upcomingCount = await db.ExamSessions.CountAsync(e => e.LecturerId == UserId && (e.Status == "Scheduled" || e.Status == "InProgress") && e.EndTime > flow.Now, ct),
            upcoming = await db.ExamSessions.Where(e => e.LecturerId == UserId && (e.Status == "Scheduled" || e.Status == "InProgress") && e.EndTime > flow.Now).OrderBy(e => e.StartTime).Take(5)
                .Select(e => new { e.ExamId, e.ExamName, e.StartTime, e.EndTime, e.Status, courseName = e.Course!.CourseName }).ToListAsync(ct) });
    }
    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse(CourseInput input, CancellationToken ct) => Data(new { courseId = await service.SaveCourse(null, UserId, input, ct) });
    [HttpPut("courses/{id:int}")]
    public async Task<IActionResult> UpdateCourse(int id, CourseInput input, CancellationToken ct) => Data(new { courseId = await service.SaveCourse(id, UserId, input, ct) });
    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass(ClassInput input, CancellationToken ct) => Data(new { classId = await service.SaveClass(null, UserId, input, ct) });
    [HttpPut("classes/{id:int}")]
    public async Task<IActionResult> UpdateClass(int id, ClassInput input, CancellationToken ct) => Data(new { classId = await service.SaveClass(id, UserId, input, ct) });

    [HttpGet("students/search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct, [FromQuery] int page = 1)
    {
        var term = q?.Trim() ?? "";
        WorkflowRules.Require(term is { Length: >= 2 and <= 100 } && page is > 0 and <= 1000, "Nhập ít nhất 2 ký tự tên/email; trang cần lớn hơn 0.");
        return Data(await db.Users.Where(u => u.Role!.RoleName == "Student" && u.Status == "Active" && (u.Email.Contains(term) || u.FullName.Contains(term)))
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Skip((page - 1) * 30).Take(30).Select(u => new { u.UserId, u.FullName, u.Email }).ToListAsync(ct));
    }
    [HttpGet("classes/{id:int}/students")]
    public async Task<IActionResult> Students(int id, CancellationToken ct)
    {
        await service.Class(id, UserId, ct);
        return Data(await db.ClassStudents.Where(s => s.ClassId == id).OrderBy(s => s.Student!.FullName)
            .Select(s => new { s.StudentId, userId = s.StudentId, s.Student!.FullName, s.Student.Email, s.Student.Status, s.JoinedAt }).ToListAsync(ct));
    }
    [HttpPost("classes/{id:int}/students/{student:int}")]
    public async Task<IActionResult> Enroll(int id, int student, CancellationToken ct) { await service.Enroll(id, UserId, student, ct); return Data(new { message = "Đã ghi danh sinh viên." }); }
    [HttpDelete("classes/{id:int}/students/{student:int}")]
    public async Task<IActionResult> Unenroll(int id, int student, CancellationToken ct) { await service.Unenroll(id, UserId, student, ct); return Data(new { message = "Đã gỡ ghi danh; lịch sử thi được giữ nguyên." }); }
    public record CsvInput(string Csv);
    [HttpPost("classes/{id:int}/students/import")]
    public async Task<IActionResult> Import(int id, CsvInput input, CancellationToken ct) => Data(await service.Import(id, UserId, input.Csv ?? "", ct));

    [HttpPost("courses/{id:int}/documents"), RequestSizeLimit(DocumentStore.MaxBytes + 65536)]
    public async Task<IActionResult> Upload(int id, IFormFile file, CancellationToken ct) => Data(new { documentId = await documents.Upload(id, UserId, file, ct) });
    [HttpDelete("documents/{id:int}")]
    public async Task<IActionResult> ArchiveDocument(int id, CancellationToken ct) { await documents.Archive(id, UserId, ct); return Data(new { message = "Đã gỡ tài liệu khỏi danh sách. File gốc được lưu giữ riêng." }); }
    [HttpPost("questions")]
    public async Task<IActionResult> CreateQuestion(QuestionInput input, CancellationToken ct) => Data(new { questionId = await service.SaveQuestion(null, UserId, input, ct) });
    [HttpPut("questions/{id:int}")]
    public async Task<IActionResult> UpdateQuestion(int id, QuestionInput input, CancellationToken ct) => Data(new { questionId = await service.SaveQuestion(id, UserId, input, ct) });
    [HttpDelete("questions/{id:int}")]
    public async Task<IActionResult> ArchiveQuestion(int id, CancellationToken ct) { await service.ArchiveQuestion(id, UserId, ct); return Data(new { message = "Đã lưu trữ câu hỏi, không xóa lịch sử." }); }
}
