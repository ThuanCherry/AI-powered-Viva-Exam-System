using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using AIVES.Api.Features.Workflow;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Exams;

public class ExamQueryService(AivesDbContext _db)
{
    public async Task<object> ListAsync(Caller caller, int? classId, CancellationToken ct)
    {
        var user = caller.UserId;
        var lecturer = caller.IsLecturer;
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
            .ToListAsync(ct);

        var result = exams.Select(e => new
        {
            e.ExamId,
            e.ExamName,
            e.Status,
            e.StartTime,
            e.EndTime,
            e.CourseName,
            e.LecturerName,
            ClassId = WorkflowService.Config(new Models.ExamSession { QuestionScopeJson = e.QuestionScopeJson }).ClassId
        });
        return result.Where(e => !classId.HasValue || e.ClassId == classId).ToArray();
    }
    public async Task<object> DetailAsync(int id, Caller caller, CancellationToken ct)
    {
        var user = caller.UserId;
        var exam = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .Include(e => e.VivaSettings)
            .Include(e => e.ExamQuestions).ThenInclude(q => q.Question)
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Student)
            .FirstOrDefaultAsync(e => e.ExamId == id, ct);

        Require(exam != null, "Exam not found.", 404);
        Require(!(caller.IsLecturer ? exam.LecturerId != user : exam.Status == "Draft" || !exam.Candidates.Any(c => c.StudentId == user && c.Status != "Cancelled")), "Không có quyền truy cập kỳ thi.", 403);

        return new
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
            Candidates = exam.Candidates.Where(c => c.Status != "Cancelled" && (caller.IsLecturer || c.StudentId == user)).OrderBy(c => c.ScheduledTime).Select(c => new
            {
                c.CandidateId,
                c.StudentId,
                StudentName = c.Student?.FullName,
                StudentEmail = c.Student?.Email,
                Questions = caller.IsLecturer ? exam.ExamQuestions.Where(q => q.CandidateId == c.CandidateId && q.ParentExamQuestionId == null).OrderBy(q => q.OrderNo).Select(q => new
                {
                    q.ExamQuestionId,
                    q.QuestionId,
                    q.OrderNo,
                    QuestionText = q.Question!.QuestionText,
                    q.Question.Difficulty,
                    q.Question.BloomLevel
                }).ToArray() : null,
                ScheduledTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value) : null,
                ScheduledEndTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value.AddMinutes(exam.TimePerStudent)) : null,
                c.Status,
                c.StartedAt,
                c.FinishedAt
            }).ToArray()
        };
    }
    public Task<bool> CanJoinAsync(int id, Caller caller, CancellationToken ct) => caller.IsLecturer
        ? _db.ExamSessions.AnyAsync(e => e.ExamId == id && e.LecturerId == caller.UserId, ct)
        : _db.ExamCandidates.AnyAsync(c => c.ExamId == id && c.StudentId == caller.UserId && c.Status != "Cancelled" && c.ExamSession!.Status != "Draft", ct);
}
