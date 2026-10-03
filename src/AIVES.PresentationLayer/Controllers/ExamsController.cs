using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.PresentationLayer.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.PresentationLayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly IExamService _examService;

    public ExamsController(IExamService examService)
    {
        _examService = examService;
    }

    /// <summary>
    /// Get all active exams.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _examService.GetAllAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get exam by ID with questions.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _examService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Get exams by subject.
    /// </summary>
    [HttpGet("subject/{subject}")]
    public async Task<IActionResult> GetBySubject(string subject)
    {
        var result = await _examService.GetBySubjectAsync(subject);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Create a new exam with questions.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamViewModel model, [FromQuery] int createdByUserId)
    {
        var exam = new Exam
        {
            Title = model.Title,
            Description = model.Description,
            Subject = model.Subject,
            CourseId = model.CourseId,
            DurationMinutes = model.DurationMinutes,
            Difficulty = model.Difficulty
        };

        var result = await _examService.CreateAsync(exam, Enumerable.Empty<Question>(), createdByUserId);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.ExamId }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// Update an existing exam.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateExamViewModel model)
    {
        var exam = new Exam
        {
            Title = model.Title,
            Description = model.Description,
            Subject = model.Subject,
            DurationMinutes = model.DurationMinutes,
            Difficulty = model.Difficulty
        };

        var result = await _examService.UpdateAsync(id, exam);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Soft-delete an exam.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _examService.DeleteAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
