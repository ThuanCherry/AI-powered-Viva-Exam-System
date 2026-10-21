using System.Security.Claims;
using AIVES.Api.Data;
using AIVES.Api.Features.Lecturer;
using AIVES.Api.Features.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Controllers;

[ApiController, Authorize(Roles = "Lecturer,Student"), Route("api/classes")]
public class ClassesController(AivesDbContext db) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int? courseId = null)
    {
        var teacher = User.IsInRole("Lecturer");
        var query = db.AcademicClasses.Where(c => c.IsActive && c.Course!.IsActive && (teacher ? c.LecturerId == UserId && c.Course.LecturerId == UserId
            : db.ClassStudents.Any(s => s.ClassId == c.ClassId && s.StudentId == UserId)));
        if (courseId.HasValue) query = query.Where(c => c.CourseId == courseId);
        return Ok(new { success = true, data = await query.OrderBy(c => c.ClassCode).Select(c => new { c.ClassId, c.ClassCode, c.ClassName, c.CourseId,
            c.Course!.CourseCode, c.Course.CourseName, studentCount = teacher ? db.ClassStudents.Count(s => s.ClassId == c.ClassId) : (int?)null }).ToListAsync(ct) });
    }
}

[ApiController, Authorize(Roles = "Lecturer,Student"), Route("api/documents")]
public class DocumentsController(AivesDbContext db, DocumentStore documents, LecturerService service) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int courseId, CancellationToken ct)
    {
        if (User.IsInRole("Lecturer")) await service.Course(courseId, UserId, ct);
        else WorkflowRules.Require(await db.ClassStudents.AnyAsync(s => s.StudentId == UserId && s.AcademicClass!.CourseId == courseId && s.AcademicClass.IsActive, ct), "Bạn chưa ghi danh môn học này.", 403);
        return Ok(new { success = true, data = await db.Documents.Where(d => d.CourseId == courseId && d.Status != "Archived").OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.DocumentId, d.CourseId, d.FileName, d.ContentType, d.FileSize, d.CreatedAt, d.Status }).ToListAsync(ct) });
    }
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var doc = await documents.Accessible(id, UserId, User.IsInRole("Lecturer"), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(documents.FilePath(doc), doc.ContentType, doc.FileName, enableRangeProcessing: true);
    }
}
