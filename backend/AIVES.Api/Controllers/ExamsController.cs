using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly AivesDbContext _db;

    public ExamsController(AivesDbContext db)
    {
        _db = db;
    }

    /// <summary>GET /api/Exams/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var exam = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .Include(e => e.VivaSettings)
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Student)
            .FirstOrDefaultAsync(e => e.ExamId == id);

        if (exam is null)
            return NotFound(new { success = false, message = "Exam not found." });

        return Ok(new
        {
            success = true,
            data = new
            {
                exam.ExamId,
                exam.ExamName,
                exam.Description,
                exam.StartTime,
                exam.EndTime,
                exam.TimePerStudent,
                exam.MainQuestionCount,
                exam.MaxFollowUpCount,
                exam.Status,
                exam.CreatedAt,
                Course = exam.Course is null ? null : new
                {
                    exam.Course.CourseId,
                    exam.Course.CourseCode,
                    exam.Course.CourseName
                },
                Lecturer = exam.Lecturer is null ? null : new
                {
                    exam.Lecturer.UserId,
                    exam.Lecturer.FullName,
                    exam.Lecturer.Email
                },
                VivaSettings = exam.VivaSettings is null ? null : new
                {
                    exam.VivaSettings.SecondsPerQuestion,
                    exam.VivaSettings.MaxFollowUpsPerQuestion
                },
                Candidates = exam.Candidates.Select(c => new
                {
                    c.CandidateId,
                    c.StudentId,
                    StudentName = c.Student?.FullName,
                    c.ScheduledTime,
                    c.Status,
                    c.StartedAt,
                    c.FinishedAt
                })
            }
        });
    }

    /// <summary>GET /api/Exams</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exams = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.ExamId,
                e.ExamName,
                e.Status,
                e.StartTime,
                e.EndTime,
                CourseName = e.Course!.CourseName,
                LecturerName = e.Lecturer!.FullName
            })
            .ToListAsync();

        return Ok(new { success = true, data = exams });
    }
}
