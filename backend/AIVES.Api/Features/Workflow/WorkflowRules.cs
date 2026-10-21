namespace AIVES.Api.Features.Workflow;

public sealed class WorkflowException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public record ExamInput(int CourseId, string ExamName, string? Description, DateTimeOffset StartTime,
    DateTimeOffset? EndTime, int TimePerStudent, int MainQuestionCount, int MaxFollowUpCount,
    int MaxFollowUpsPerQuestion, int SecondsPerQuestion, string Strategy, int[] StudentIds, int? ClassId = null);
public record AnswerInput(string TurnId, string Transcript);
public record ScopeConfig(string Strategy = "Random", string TimeZone = "Asia/Ho_Chi_Minh", string StorageTimeZone = "UTC", int? ClassId = null);

public static class WorkflowRules
{
    public static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message, int status = 400)
    {
        if (!condition) throw new WorkflowException(status, message);
    }

    public static void Validate(ExamInput input, int poolSize)
    {
        Require(!string.IsNullOrWhiteSpace(input.ExamName) && input.ExamName.Length <= 200, "Tên kỳ thi cần từ 1–200 ký tự.");
        Require(input.Description?.Length is not > 1000, "Mô tả tối đa 1000 ký tự.");
        _ = ScheduleEnd(input);
        Require(input.MainQuestionCount is >= 1 and <= 20 && input.MainQuestionCount <= poolSize, "Ngân hàng câu chính Approved không đủ hoặc số câu ngoài 1–20.");
        Require(input.MaxFollowUpCount is >= 0 and <= 100 && input.MaxFollowUpsPerQuestion is >= 0 and <= 5, "Giới hạn follow-up không hợp lệ (tổng 0–100, mỗi câu 0–5).");
        Require(input.SecondsPerQuestion is >= 15 and <= 1800, "Thời gian từng lượt cần từ 15–1800 giây.");
        Require(input.Strategy is "Random" or "Adaptive", "Chiến lược cần là Random hoặc Adaptive.");
    }

    // EndTime from older clients is accepted for compatibility, never trusted when saving.
    public static DateTimeOffset ScheduleEnd(ExamInput input)
    {
        Require(input.TimePerStudent is > 0 and <= 180, "Mỗi slot cần từ 1–180 phút.");
        Require(input.StudentIds is { Length: > 0 and <= 500 } && input.StudentIds.Distinct().Count() == input.StudentIds.Length, "Chọn 1–500 sinh viên, không lặp.");
        Require(input.StartTime.Year >= 1000, "Ngày bắt đầu không hợp lệ.");
        try { return input.StartTime.ToUniversalTime().AddMinutes((long)input.TimePerStudent * input.StudentIds.Length); }
        catch (ArgumentOutOfRangeException) { throw new WorkflowException(400, "Lịch thi vượt phạm vi ngày giờ cho phép."); }
    }

    public static bool Overlaps(DateTime a, DateTime b, DateTime c, DateTime d) => a < d && c < b;
    public static bool CanFollowUp(int total, int perMain, int totalLimit, int perMainLimit, DateTime now, DateTime deadline)
        => total < totalLimit && perMain < perMainLimit && now < deadline;

    // Prefer questions absent from the last two candidates, then least-used questions.
    // Adaptive is a documented difficulty-balanced allocation, without fabricated ability scores.
    public static List<Models.Question> Allocate(List<Models.Question> pool, int count, IEnumerable<int> recent,
        IReadOnlyDictionary<int, int> usage, string strategy)
    {
        Require(pool.Count >= count, "Pool không đủ câu chính.");
        var recentIds = recent.ToHashSet();
        var ranked = pool.OrderBy(q => recentIds.Contains(q.QuestionId))
            .ThenBy(q => usage.GetValueOrDefault(q.QuestionId)).ThenBy(_ => Random.Shared.Next()).ToList();
        if (strategy != "Adaptive") return ranked.Take(count).ToList();
        var selected = new List<Models.Question>();
        var levels = new[] { "Easy", "Medium", "Hard" };
        for (var i = 0; i < count; i++)
        {
            var available = ranked.Where(q => !selected.Contains(q)).ToList();
            selected.Add(available.FirstOrDefault(q => q.Difficulty == levels[i % 3] && !recentIds.Contains(q.QuestionId))
                ?? available.FirstOrDefault(q => !recentIds.Contains(q.QuestionId)) ?? available[0]);
        }
        return selected;
    }
}
