using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Controllers;

[ApiController]
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
        [FromQuery] string? bloomLevel)
    {
        var query = _db.Questions
            .Include(q => q.Course)
            .Include(q => q.Options)
            .AsQueryable();

        if (courseId.HasValue)
            query = query.Where(q => q.CourseId == courseId.Value);
        if (!string.IsNullOrEmpty(difficulty))
            query = query.Where(q => q.Difficulty == difficulty);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(q => q.Status == status);
        if (!string.IsNullOrEmpty(bloomLevel))
            query = query.Where(q => q.BloomLevel == bloomLevel);

        var questions = await query
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new
            {
                q.QuestionId,
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
            return NotFound(new { success = false, message = "Question not found." });

        return Ok(new
        {
            success = true,
            data = new
            {
                q.QuestionId,
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
