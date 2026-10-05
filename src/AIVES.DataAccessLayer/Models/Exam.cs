using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AIVES.DataAccessLayer.Models;
[Table("exams")]
public class Exam
{
    [Key, Column("ExamId")] public long ExamId { get; set; }
    [Column("course_id")] public long CourseId { get; set; }
    [Column("created_by")] public long CreatedByUserId { get; set; }
    [Column("title"), MaxLength(255)] public string Title { get; set; } = "";
    [Column("description")] public string? Description { get; set; }
    [Column("starts_at")] public DateTime StartsAt { get; set; }
    [Column("ends_at")] public DateTime EndsAt { get; set; }
    [Column("duration_minutes_per_student")] public int DurationMinutesPerStudent { get; set; } = 15;
    [Column("main_question_count")] public int MainQuestionCount { get; set; } = 3;
    [Column("max_follow_up_per_question")] public int MaxFollowUpPerQuestion { get; set; } = 2;
    [Column("selection_strategy")] public string SelectionStrategy { get; set; } = "RANDOM";
    [Column("avoid_recent_duplicate_count")] public int AvoidRecentDuplicateCount { get; set; } = 1;
    [Column("status")] public string Status { get; set; } = "DRAFT";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}