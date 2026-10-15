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
    public async Task<IActionResult> GetAll()
    {
        var courses = await _db.Courses
            .Include(c => c.Lecturer)
            .Where(c => c.IsActive)
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
}
