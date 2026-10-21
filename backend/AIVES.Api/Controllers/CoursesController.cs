using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AIVES.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Lecturer,Student")]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly AivesDbContext _db;

    public CoursesController(AivesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? lecturerId = null)
    {
        var user = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var teacher = User.IsInRole("Lecturer");
        var courses = await _db.Courses
            .Include(c => c.Lecturer)
            .Where(c => c.IsActive && (User.IsInRole("Admin") ? !lecturerId.HasValue || c.LecturerId == lecturerId : teacher ? c.LecturerId == user : _db.ClassStudents.Any(s => s.StudentId == user && s.AcademicClass!.CourseId == c.CourseId && s.AcademicClass.IsActive)))
            .OrderBy(c => c.CourseName)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.LecturerId,
                LecturerName = c.Lecturer!.FullName,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = courses });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var course = await _db.Courses
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.CourseId == id);

        if (course is null)
            return NotFound(new { success = false, message = "Không tìm thấy môn học." });
        if (!User.IsInRole("Admin") && (User.IsInRole("Lecturer") ? course.LecturerId != user : !await _db.ClassStudents.AnyAsync(s => s.StudentId == user && s.AcademicClass!.CourseId == id && s.AcademicClass.IsActive))) return Forbid();

        return Ok(new
        {
            success = true,
            data = new
            {
                course.CourseId,
                course.CourseCode,
                course.CourseName,
                course.Description,
                course.IsActive,
                LecturerName = course.Lecturer?.FullName,
                course.CreatedAt
            }
        });
    }

    // Preserve dev's existing route; only Admin can assign an existing lecturer.
    [HttpPut("{id:int}/assign-lecturer")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignLecturer(int id, [FromBody] AssignLecturerRequest request)
    {
        var course = await _db.Courses.Include(c => c.Lecturer).FirstOrDefaultAsync(c => c.CourseId == id);
        if (course == null) return NotFound(new { success = false, message = "Không tìm thấy môn học." });
        if (!request.LecturerId.HasValue || !await _db.Users.AnyAsync(u => u.UserId == request.LecturerId && u.Status == "Active" && u.Role!.RoleName == "Lecturer"))
            return BadRequest(new { success = false, message = "Chọn giảng viên đang hoạt động." });
        if (course.LecturerId != request.LecturerId && (await _db.AcademicClasses.AnyAsync(c => c.CourseId == id) || await _db.ExamSessions.AnyAsync(e => e.CourseId == id)))
            return Conflict(new { success = false, message = "Môn đã có lớp hoặc kỳ thi; cần chuyển giao nghiệp vụ trước khi đổi giảng viên." });
        course.LecturerId = request.LecturerId.Value; course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); await _db.Entry(course).Reference(c => c.Lecturer).LoadAsync();
        return Ok(new { success = true, data = new { course.CourseId, course.CourseCode, course.CourseName, course.LecturerId, LecturerName = course.Lecturer?.FullName } });
    }
}

public record AssignLecturerRequest(int? LecturerId);
