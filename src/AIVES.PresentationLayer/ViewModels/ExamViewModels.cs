using System.ComponentModel.DataAnnotations;
using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;
namespace AIVES.PresentationLayer.ViewModels;
public class CreateExamViewModel
{
    [Range(1, long.MaxValue)] public long CourseId { get; set; }
    [Required, StringLength(255)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; } = DateTime.Today.AddDays(1).AddHours(8);
    public DateTime EndsAt { get; set; } = DateTime.Today.AddDays(1).AddHours(11);
    [Range(1, 1440)] public int DurationMinutesPerStudent { get; set; } = 15;
    [Range(1, 1000)] public int MainQuestionCount { get; set; } = 3;
    [Range(0, 1000)] public int MaxFollowUpPerQuestion { get; set; } = 2;
    [Required, RegularExpression("^(RANDOM|ADAPTIVE)$")] public string SelectionStrategy { get; set; } = "RANDOM";
    [Range(0, 1000)] public int AvoidRecentDuplicateCount { get; set; } = 1;
    public IReadOnlyList<Course> Courses { get; set; } = Array.Empty<Course>();
    public ExamCommand ToCommand() => new(CourseId, Title, Description, StartsAt, EndsAt, DurationMinutesPerStudent,
        MainQuestionCount, MaxFollowUpPerQuestion, SelectionStrategy, AvoidRecentDuplicateCount);
    public static CreateExamViewModel From(Exam e) => new()
    {
        CourseId = e.CourseId, Title = e.Title, Description = e.Description, StartsAt = e.StartsAt, EndsAt = e.EndsAt,
        DurationMinutesPerStudent = e.DurationMinutesPerStudent, MainQuestionCount = e.MainQuestionCount,
        MaxFollowUpPerQuestion = e.MaxFollowUpPerQuestion, SelectionStrategy = e.SelectionStrategy,
        AvoidRecentDuplicateCount = e.AvoidRecentDuplicateCount
    };
}
public class ParticipantForm
{
    public List<long> StudentIds { get; set; } = new();
}
public class QuestionPoolForm
{
    public List<long> QuestionIds { get; set; } = new();
}
public class ScheduleViewModel
{
    public List<SlotViewModel> Slots { get; set; } = new();
}
public class SlotViewModel
{
    public long ParticipantId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}
public class ScheduleSettingsViewModel
{
    public DateTime? StartsAt { get; set; }
    [Range(1, 1440)] public int? DurationMinutesPerStudent { get; set; }
}
