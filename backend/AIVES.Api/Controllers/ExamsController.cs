using AIVES.Api.Data;
using AIVES.Api.Features.Exams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly AivesDbContext _db;
    private readonly ExamService _examService;

    public ExamsController(AivesDbContext db, ExamService examService)
    {
        _db = db;
        _examService = examService;
    }

    /// <summary>GET /api/Exams</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exams = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .Include(e => e.Candidates)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.ExamId,
                e.ExamName,
                e.Description,
                e.Status,
                e.StartTime,
                e.EndTime,
                e.TimePerStudent,
                e.MainQuestionCount,
                e.MaxFollowUpCount,
                e.CreatedAt,
                CandidateCount = e.Candidates.Count,
                CourseId = e.CourseId,
                CourseName = e.Course != null ? e.Course.CourseName : string.Empty,
                CourseCode = e.Course != null ? e.Course.CourseCode : string.Empty,
                LecturerName = e.Lecturer != null ? e.Lecturer.FullName : string.Empty
            })
            .ToListAsync();

        return Ok(new { success = true, data = exams });
    }

    /// <summary>GET /api/Exams/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var exam = await _examService.GetExamByIdAsync(id);
        if (exam is null)
            return NotFound(new { success = false, message = "Exam not found." });

        return Ok(new { success = true, data = exam });
    }

    /// <summary>POST /api/Exams - Tạo kỳ thi mới (Draft)</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamRequest request)
    {
        try
        {
            // Default to 1 (Admin/Lecturer) if no claims
            int currentUserId = 1;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var parsedId))
            {
                currentUserId = parsedId;
            }

            var examId = await _examService.CreateExamAsync(request, currentUserId);
            var created = await _examService.GetExamByIdAsync(examId);

            return CreatedAtAction(nameof(GetById), new { id = examId }, new
            {
                success = true,
                message = "Exam session created successfully in Draft status.",
                data = created
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>POST /api/Exams/{id}/candidates - Gán sinh viên và lịch thi (Scheduled)</summary>
    [HttpPost("{id:int}/candidates")]
    public async Task<IActionResult> AssignCandidates(int id, [FromBody] AssignCandidatesRequest request)
    {
        try
        {
            var success = await _examService.AssignCandidatesAsync(id, request);
            if (!success)
                return NotFound(new { success = false, message = "Exam not found." });

            var updated = await _examService.GetExamByIdAsync(id);
            return Ok(new
            {
                success = true,
                message = "Candidates and schedule updated successfully.",
                data = updated
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>POST /api/Exams/{id}/generate-questions - Phân bổ câu hỏi chống trùng lặp liên tiếp (Ready)</summary>
    [HttpPost("{id:int}/generate-questions")]
    public async Task<IActionResult> GenerateQuestions(int id, [FromBody] ConfigureQuestionsRequest request)
    {
        try
        {
            var assignedCount = await _examService.GenerateQuestionsAsync(id, request);
            var updated = await _examService.GetExamByIdAsync(id);

            return Ok(new
            {
                success = true,
                message = $"Successfully distributed questions to all candidates without consecutive duplicates ({assignedCount} question assignments). Exam is now Ready.",
                data = updated
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>PUT /api/Exams/{id}/status - Cập nhật trạng thái kỳ thi</summary>
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateExamStatusRequest request)
    {
        try
        {
            var success = await _examService.UpdateStatusAsync(id, request.Status);
            if (!success)
                return NotFound(new { success = false, message = "Exam not found." });

            return Ok(new
            {
                success = true,
                message = $"Exam status updated to '{request.Status}' successfully."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
