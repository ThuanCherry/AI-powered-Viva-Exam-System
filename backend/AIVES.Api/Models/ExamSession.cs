using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("exam_sessions")]
public class ExamSession
{
    [Key]
    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("course_id")]
    public int CourseId { get; set; }

    [Column("lecturer_id")]
    public int LecturerId { get; set; }

    [Column("exam_name")]
    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [Column("time_per_student")]
    public int TimePerStudent { get; set; }

    [Column("main_question_count")]
    public int MainQuestionCount { get; set; } = 3;

    [Column("max_follow_up_count")]
    public int MaxFollowUpCount { get; set; } = 2;

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("question_scope_json")]
    public string? QuestionScopeJson { get; set; }

    // Navigation
    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [ForeignKey(nameof(LecturerId))]
    public User? Lecturer { get; set; }

    public ExamVivaSettings? VivaSettings { get; set; }
    public ICollection<ExamCandidate> Candidates { get; set; } = [];
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = [];
}
