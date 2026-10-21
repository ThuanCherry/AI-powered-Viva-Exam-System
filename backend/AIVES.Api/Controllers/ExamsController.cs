using AIVES.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AIVES.Api.Features.Workflow;

namespace AIVES.Api.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer,Student")]
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
        var user = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var exam = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .Include(e => e.VivaSettings)
            .Include(e => e.ExamQuestions).ThenInclude(q => q.Question)
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Student)
            .FirstOrDefaultAsync(e => e.ExamId == id);

        if (exam is null)
            return NotFound(new { success = false, message = "Exam not found." });
        if (User.IsInRole("Lecturer") ? exam.LecturerId != user : exam.Status == "Draft" || !exam.Candidates.Any(c => c.StudentId == user && c.Status != "Cancelled")) return Forbid();

        return Ok(new
        {
            success = true,
            data = new
            {
                exam.ExamId,
                exam.ExamName,
                exam.Description,
                StartTime = WorkflowService.Iso(exam.StartTime),
                EndTime = WorkflowService.Iso(exam.EndTime),
                exam.CourseId,
                ClassId = WorkflowService.Config(exam).ClassId,
                Strategy = WorkflowService.Config(exam).Strategy,
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
                Candidates = exam.Candidates.Where(c => c.Status != "Cancelled" && (User.IsInRole("Lecturer") || c.StudentId == user)).OrderBy(c => c.ScheduledTime).Select(c => new
                {
                    c.CandidateId,
                    c.StudentId,
                    StudentName = c.Student?.FullName,
                    StudentEmail = c.Student?.Email,
                    Questions = User.IsInRole("Lecturer") ? exam.ExamQuestions.Where(q => q.CandidateId == c.CandidateId && q.ParentExamQuestionId == null).OrderBy(q => q.OrderNo).Select(q => new {
                        q.ExamQuestionId, q.QuestionId, q.OrderNo, QuestionText = q.Question!.QuestionText,
                        q.Question.Difficulty, q.Question.BloomLevel
                    }) : null,
                    ScheduledTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value) : null,
                    ScheduledEndTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value.AddMinutes(exam.TimePerStudent)) : null,
                    c.Status,
                    c.StartedAt,
                    c.FinishedAt
                })
            }
        });
    }

    /// <summary>GET /api/Exams</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? classId = null)
    {
        var user = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var lecturer = User.IsInRole("Lecturer");
        var exams = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .Where(e => lecturer ? e.LecturerId == user : e.Status != "Draft" && e.Candidates.Any(c => c.StudentId == user && c.Status != "Cancelled"))
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.ExamId,
                e.ExamName,
                e.Status,
                e.StartTime,
                e.EndTime,
                e.QuestionScopeJson,
                CourseName = e.Course!.CourseName,
                LecturerName = e.Lecturer!.FullName
            })
            .ToListAsync();

        var result = exams.Select(e => new { e.ExamId, e.ExamName, e.Status, e.StartTime, e.EndTime, e.CourseName, e.LecturerName,
            ClassId = WorkflowService.Config(new Models.ExamSession { QuestionScopeJson = e.QuestionScopeJson }).ClassId });
        return Ok(new { success = true, data = result.Where(e => !classId.HasValue || e.ClassId == classId) });
    }
}
