using AIVES.BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.PresentationLayer.Controllers;

public record StartSessionRequest(int ExamId, int StudentUserId);
public record SubmitAnswerRequest(int SessionId, int QuestionId, string AnswerText, string? AudioFilePath = null);

[ApiController]
[Route("api/[controller]")]
public class ExamSessionsController : ControllerBase
{
    private readonly IExamSessionService _sessionService;

    public ExamSessionsController(IExamSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// Get session by ID with all answers.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _sessionService.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Get all sessions for a student.
    /// </summary>
    [HttpGet("student/{studentUserId}")]
    public async Task<IActionResult> GetByStudent(int studentUserId)
    {
        var result = await _sessionService.GetByStudentAsync(studentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Get all results for an exam.
    /// </summary>
    [HttpGet("exam/{examId}/results")]
    public async Task<IActionResult> GetResultsByExam(int examId)
    {
        var result = await _sessionService.GetResultsByExamAsync(examId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Start a new exam session.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
    {
        var result = await _sessionService.StartSessionAsync(request.ExamId, request.StudentUserId);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.SessionId }, result)
            : BadRequest(result);
    }

    /// <summary>
    /// Submit an answer for evaluation.
    /// </summary>
    [HttpPost("submit-answer")]
    public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAnswerRequest request)
    {
        var result = await _sessionService.SubmitAnswerAsync(
            request.SessionId,
            request.QuestionId,
            request.AnswerText,
            request.AudioFilePath);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Complete an exam session and calculate final score.
    /// </summary>
    [HttpPut("{id}/complete")]
    public async Task<IActionResult> CompleteSession(int id)
    {
        var result = await _sessionService.CompleteSessionAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
