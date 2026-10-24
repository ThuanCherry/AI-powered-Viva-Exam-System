using AIVES.Api.Features.Lecturer;
using AIVES.Api.Features.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Lecturer"), Route("api/lecturer")]
public class LecturerController(LecturerService service, DocumentStore documents) : ApiControllerBase
{

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        return Data(await service.DashboardAsync(UserId, ct));
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
        return Data(await service.SearchStudentsAsync(q, page, ct));
    }
    [HttpGet("classes/{id:int}/students")]
    public async Task<IActionResult> Students(int id, CancellationToken ct)
    {
        return Data(await service.ClassStudentsAsync(id, UserId, ct));
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
