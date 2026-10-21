using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AIVES.Api.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer")]
[Route("api/[controller]")]
public class QuestionsController : ControllerBase
{
    private readonly AivesDbContext _db;

    public QuestionsController(AivesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? courseId,
        [FromQuery] string? difficulty,
        [FromQuery] string? status,
        [FromQuery] string? bloomLevel, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        if (page < 1 || page > 10000 || pageSize < 1 || pageSize > 100) return BadRequest(new { success = false, message = "Phân trang không hợp lệ." });
        var query = _db.Questions
            .Include(q => q.Course)
            .Include(q => q.Options)
            .Where(q => q.Course!.LecturerId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
            .AsQueryable();

        if (courseId.HasValue)
            query = query.Where(q => q.CourseId == courseId.Value);
        if (!string.IsNullOrEmpty(difficulty))
            query = query.Where(q => q.Difficulty == difficulty);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(q => q.Status == status);
        if (!string.IsNullOrEmpty(bloomLevel))
            query = query.Where(q => q.BloomLevel == bloomLevel);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(item => item.QuestionText.Contains(q));
        if (string.IsNullOrEmpty(status)) query = query.Where(item => item.Status != "Archived");

        var questions = await query
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.QuestionId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(q => new
            {
                q.QuestionId,
                q.CourseId,
                q.QuestionText,
                q.ExpectedAnswer,
                q.BloomLevel,
                q.Difficulty,
                q.QuestionType,
                q.Status,
                q.Source,
                CourseName = q.Course!.CourseName,
                q.CreatedAt,
                Options = q.Options.OrderBy(o => o.DisplayOrder).Select(o => new
                {
                    o.OptionId,
                    o.OptionText,
                    o.IsCorrect,
                    o.DisplayOrder
                })
            })
            .ToListAsync();

        return Ok(new { success = true, data = questions });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var q = await _db.Questions
            .Include(q => q.Course)
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.QuestionId == id);

        if (q is null)
            return NotFound(new { success = false, message = "Không tìm thấy câu hỏi." });
        if (q.Course?.LecturerId != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)) return Forbid();

        return Ok(new
        {
            success = true,
            data = new
            {
                q.QuestionId,
                q.CourseId,
                q.QuestionText,
                q.ExpectedAnswer,
                q.BloomLevel,
                q.Difficulty,
                q.QuestionType,
                q.Status,
                q.Source,
                CourseName = q.Course?.CourseName,
                q.CreatedAt,
                Options = q.Options.OrderBy(o => o.DisplayOrder).Select(o => new
                {
                    o.OptionId,
                    o.OptionText,
                    o.IsCorrect,
                    o.DisplayOrder
                })
            }
        });
    }
}
