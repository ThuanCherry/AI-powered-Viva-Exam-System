using System.Text.Json;
using AIVES.Api.Data;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Features.Exams;

public class ExamService
{
    private readonly AivesDbContext _db;

    public ExamService(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<ExamDetailDto?> GetExamByIdAsync(int id)
    {
        var exam = await _db.ExamSessions
            .Include(e => e.Course)
            .Include(e => e.Lecturer)
            .Include(e => e.VivaSettings)
            .Include(e => e.Candidates)
                .ThenInclude(c => c.Student)
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.ExamId == id);

        if (exam is null) return null;

        var questionsByCandidate = exam.ExamQuestions
            .GroupBy(eq => eq.CandidateId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(eq => eq.OrderNo).Select(eq => new AssignedQuestionDto(
                    eq.ExamQuestionId,
                    eq.QuestionId,
                    eq.Question?.QuestionText ?? string.Empty,
                    eq.Question?.Difficulty ?? string.Empty,
                    eq.Question?.BloomLevel ?? string.Empty,
                    eq.OrderNo
                )).ToList()
            );

        return new ExamDetailDto(
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
            exam.QuestionScopeJson,
            exam.Course is null ? null : new CourseSummaryDto(exam.Course.CourseId, exam.Course.CourseCode, exam.Course.CourseName),
            exam.Lecturer is null ? null : new LecturerSummaryDto(exam.Lecturer.UserId, exam.Lecturer.FullName, exam.Lecturer.Email),
            exam.VivaSettings is null ? null : new VivaSettingsDto(exam.VivaSettings.SecondsPerQuestion, exam.VivaSettings.MaxFollowUpsPerQuestion),
            exam.Candidates.OrderBy(c => c.ScheduledTime).Select(c => new CandidateDto(
                c.CandidateId,
                c.StudentId,
                c.Student?.FullName,
                c.Student?.Email,
                c.ScheduledTime,
                c.Status,
                c.StartedAt,
                c.FinishedAt,
                questionsByCandidate.TryGetValue(c.CandidateId, out var qs) ? qs : new List<AssignedQuestionDto>()
            )).ToList(),
            exam.ExamQuestions.Count
        );
    }

    public async Task<int> CreateExamAsync(CreateExamRequest request, int defaultLecturerId)
    {
        var course = await _db.Courses.FindAsync(request.CourseId);
        if (course is null)
        {
            throw new ArgumentException($"Course with ID {request.CourseId} does not exist.");
        }

        int lecturerId = (request.LecturerId.HasValue && request.LecturerId.Value > 0)
            ? request.LecturerId.Value
            : (defaultLecturerId > 0 ? defaultLecturerId : course.LecturerId);

        var exam = new ExamSession
        {
            CourseId = request.CourseId,
            LecturerId = lecturerId,
            ExamName = request.ExamName,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            TimePerStudent = request.TimePerStudent > 0 ? request.TimePerStudent : 20,
            MainQuestionCount = request.MainQuestionCount > 0 ? request.MainQuestionCount : 3,
            MaxFollowUpCount = request.MaxFollowUpCount >= 0 ? request.MaxFollowUpCount : 2,
            Status = ExamSession.Statuses.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.ExamSessions.Add(exam);
        await _db.SaveChangesAsync();

        var vivaSettings = new ExamVivaSettings
        {
            ExamId = exam.ExamId,
            SecondsPerQuestion = request.SecondsPerQuestion >= 15 ? request.SecondsPerQuestion : 120,
            MaxFollowUpsPerQuestion = Math.Clamp(request.MaxFollowUpCount, 0, 5)
        };
        _db.ExamVivaSettings.Add(vivaSettings);
        await _db.SaveChangesAsync();

        return exam.ExamId;
    }

    public async Task<bool> AssignCandidatesAsync(int examId, AssignCandidatesRequest request)
    {
        var exam = await _db.ExamSessions
            .Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.ExamId == examId);

        if (exam is null) return false;

        var studentIds = request.Candidates.Select(c => c.StudentId).Distinct().ToList();
        var validStudents = await _db.Users
            .Where(u => studentIds.Contains(u.UserId))
            .Select(u => u.UserId)
            .ToListAsync();

        var existingCandidates = exam.Candidates.ToList();
        _db.ExamCandidates.RemoveRange(existingCandidates);

        foreach (var item in request.Candidates)
        {
            if (!validStudents.Contains(item.StudentId)) continue;

            _db.ExamCandidates.Add(new ExamCandidate
            {
                ExamId = examId,
                StudentId = item.StudentId,
                ScheduledTime = item.ScheduledTime,
                Status = "Waiting"
            });
        }

        if (exam.Status == ExamSession.Statuses.Draft && request.Candidates.Count > 0)
        {
            exam.Status = ExamSession.Statuses.Scheduled;
        }

        exam.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> GenerateQuestionsAsync(int examId, ConfigureQuestionsRequest request)
    {
        var exam = await _db.ExamSessions
            .Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.ExamId == examId);

        if (exam is null)
            throw new ArgumentException($"Exam with ID {examId} not found.");

        if (request.MainQuestionCount.HasValue && request.MainQuestionCount.Value > 0)
        {
            exam.MainQuestionCount = request.MainQuestionCount.Value;
        }
        if (request.MaxFollowUpCount.HasValue && request.MaxFollowUpCount.Value >= 0)
        {
            exam.MaxFollowUpCount = request.MaxFollowUpCount.Value;
            var viva = await _db.ExamVivaSettings.FirstOrDefaultAsync(v => v.ExamId == examId);
            if (viva != null)
            {
                viva.MaxFollowUpsPerQuestion = Math.Clamp(request.MaxFollowUpCount.Value, 0, 5);
            }
        }

        var candidates = exam.Candidates
            .OrderBy(c => c.ScheduledTime ?? DateTime.MaxValue)
            .ThenBy(c => c.CandidateId)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("Cannot generate questions without candidates. Please assign students first.");
        }

        IQueryable<Question> query = _db.Questions.Where(q => q.CourseId == exam.CourseId);
        if (request.SelectedQuestionIds != null && request.SelectedQuestionIds.Count > 0)
        {
            query = query.Where(q => request.SelectedQuestionIds.Contains(q.QuestionId));
        }

        var pool = await query.ToListAsync();
        if (pool.Count == 0)
        {
            throw new InvalidOperationException("No questions found in pool for this exam/course.");
        }

        int k = exam.MainQuestionCount;
        if (pool.Count < k)
        {
            throw new InvalidOperationException($"The question pool only has {pool.Count} questions, but {k} questions are required per candidate.");
        }

        var random = new Random();
        var previousAssignedIds = new HashSet<int>();
        var newExamQuestions = new List<ExamQuestion>();

        foreach (var candidate in candidates)
        {
            List<Question> selectedForCandidate;

            if (string.Equals(request.Strategy, "ADAPTIVE", StringComparison.OrdinalIgnoreCase))
            {
                // Thích ứng: Tránh trùng lặp với thí sinh liền trước, ưu tiên phân bổ câu hỏi từ mức độ Medium/Understand
                var nonOverlapPool = pool.Where(q => !previousAssignedIds.Contains(q.QuestionId)).ToList();
                if (nonOverlapPool.Count >= k)
                {
                    var medium = nonOverlapPool.Where(q => q.Difficulty == "Medium").OrderBy(_ => random.Next()).ToList();
                    var others = nonOverlapPool.Where(q => q.Difficulty != "Medium").OrderBy(_ => random.Next()).ToList();
                    selectedForCandidate = medium.Concat(others).Take(k).ToList();
                }
                else
                {
                    var needed = k - nonOverlapPool.Count;
                    var overlapFill = pool.Where(q => previousAssignedIds.Contains(q.QuestionId)).OrderBy(_ => random.Next()).Take(needed).ToList();
                    selectedForCandidate = nonOverlapPool.Concat(overlapFill).ToList();
                }
            }
            else // "RANDOM"
            {
                // Ngẫu nhiên chống trùng lặp giữa các thí sinh thi liên tiếp:
                var nonOverlapPool = pool.Where(q => !previousAssignedIds.Contains(q.QuestionId)).OrderBy(_ => random.Next()).ToList();
                if (nonOverlapPool.Count >= k)
                {
                    selectedForCandidate = nonOverlapPool.Take(k).ToList();
                }
                else
                {
                    // Nếu pool < 2*K: dùng toàn bộ non-overlap và lấp bù câu hỏi trùng ít nhất có thể
                    var needed = k - nonOverlapPool.Count;
                    var overlapFill = pool.Where(q => previousAssignedIds.Contains(q.QuestionId)).OrderBy(_ => random.Next()).Take(needed).ToList();
                    selectedForCandidate = nonOverlapPool.Concat(overlapFill).OrderBy(_ => random.Next()).ToList();
                }
            }

            previousAssignedIds = selectedForCandidate.Select(q => q.QuestionId).ToHashSet();

            for (int order = 0; order < selectedForCandidate.Count; order++)
            {
                newExamQuestions.Add(new ExamQuestion
                {
                    ExamId = exam.ExamId,
                    CandidateId = candidate.CandidateId,
                    QuestionId = selectedForCandidate[order].QuestionId,
                    OrderNo = order + 1,
                    IsCompleted = false
                });
            }
        }

        var oldQuestions = await _db.ExamQuestions.Where(eq => eq.ExamId == exam.ExamId).ToListAsync();
        _db.ExamQuestions.RemoveRange(oldQuestions);
        _db.ExamQuestions.AddRange(newExamQuestions);

        exam.QuestionScopeJson = JsonSerializer.Serialize(new
        {
            strategy = request.Strategy,
            selectedQuestionIds = pool.Select(q => q.QuestionId).ToList(),
            generatedAt = DateTime.UtcNow,
            totalAssigned = newExamQuestions.Count
        });

        exam.Status = ExamSession.Statuses.Ready;
        exam.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return newExamQuestions.Count;
    }

    public async Task<bool> UpdateStatusAsync(int examId, string newStatus)
    {
        if (!ExamSession.Statuses.All.Contains(newStatus))
        {
            throw new ArgumentException($"Invalid status: '{newStatus}'. Allowed: {string.Join(", ", ExamSession.Statuses.All)}");
        }

        var exam = await _db.ExamSessions.FindAsync(examId);
        if (exam is null) return false;

        exam.Status = newStatus;
        exam.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
