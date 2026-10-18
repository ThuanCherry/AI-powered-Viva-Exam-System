using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly AivesDbContext _db;

    public CoursesController(AivesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? lecturerId)
    {
        var query = _db.Courses
            .Include(c => c.Lecturer)
            .Where(c => c.IsActive);

        if (lecturerId.HasValue)
        {
            query = query.Where(c => c.LecturerId == lecturerId.Value);
        }

        var courses = await query
            .OrderBy(c => c.CourseName)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.LecturerId,
                LecturerName = c.Lecturer != null ? c.Lecturer.FullName : string.Empty,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = courses });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var course = await _db.Courses
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.CourseId == id);

        if (course is null)
            return NotFound(new { success = false, message = "Course not found." });

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

    /// <summary>PUT /api/Courses/{id}/assign-lecturer - Phân quyền giảng viên cho môn học</summary>
    [HttpPut("{id:int}/assign-lecturer")]
    public async Task<IActionResult> AssignLecturer(int id, [FromBody] AssignLecturerRequest request)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course is null)
            return NotFound(new { success = false, message = "Course not found." });

        if (request.LecturerId.HasValue)
        {
            course.LecturerId = request.LecturerId.Value;
        }

        course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var updated = await _db.Courses.Include(c => c.Lecturer).FirstOrDefaultAsync(c => c.CourseId == id);

        return Ok(new
        {
            success = true,
            message = "Lecturer assigned to course successfully.",
            data = new
            {
                updated?.CourseId,
                updated?.CourseCode,
                updated?.CourseName,
                LecturerName = updated?.Lecturer?.FullName ?? "Unassigned"
            }
        });
    }
}

public record AssignLecturerRequest(int? LecturerId);

