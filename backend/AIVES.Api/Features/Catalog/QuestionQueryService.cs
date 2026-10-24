using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using AIVES.Api.Features.Workflow;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Catalog;

public class QuestionQueryService(AivesDbContext _db)
{
    public async Task<object> ListAsync(int lecturer, int? courseId, string? difficulty, string? status, string? bloomLevel, string? q, int page, int pageSize, CancellationToken ct)
    {
        Require(page is >= 1 and <= 10000 && pageSize is >= 1 and <= 100, "Phân trang không hợp lệ.");
        var query = _db.Questions
            .Include(q => q.Course)
            .Include(q => q.Options)
            .Where(q => q.Course!.LecturerId == lecturer)
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
                }).ToArray()
            })
            .ToListAsync(ct);

        return questions;
    }
    public async Task<object> DetailAsync(int id, int lecturer, CancellationToken ct)
    {
        var q = await _db.Questions
            .Include(q => q.Course)
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.QuestionId == id, ct);

        Require(q != null, "Không tìm thấy câu hỏi.", 404);
        Require(q.Course?.LecturerId == lecturer, "Bạn không quản lý câu hỏi này.", 403);

        return new
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
                }).ToArray()
            };
    }
}
