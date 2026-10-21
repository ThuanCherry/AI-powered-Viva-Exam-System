using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AIVES.Api.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer,Student")]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly AivesDbContext _db;

    public CoursesController(AivesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var user = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var teacher = User.IsInRole("Lecturer");
        var courses = await _db.Courses
            .Include(c => c.Lecturer)
            .Where(c => c.IsActive && (teacher ? c.LecturerId == user : _db.ClassStudents.Any(s => s.StudentId == user && s.AcademicClass!.CourseId == c.CourseId && s.AcademicClass.IsActive)))
            .OrderBy(c => c.CourseName)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Description,
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
        if (User.IsInRole("Lecturer") ? course.LecturerId != user : !await _db.ClassStudents.AnyAsync(s => s.StudentId == user && s.AcademicClass!.CourseId == id && s.AcademicClass.IsActive)) return Forbid();

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
}
