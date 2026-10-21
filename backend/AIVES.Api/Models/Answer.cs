using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("answers")]
public class Answer
{
    [Key]
    [Column("answer_id")]
    public int AnswerId { get; set; }

    [Column("exam_question_id")]
    public int ExamQuestionId { get; set; }

    [Column("candidate_id")]
    public int CandidateId { get; set; }

    [Column("selected_option_id")]
    public int? SelectedOptionId { get; set; }

    [Column("transcript")]
    public string? Transcript { get; set; }

    [Column("audio_path")]
    [MaxLength(1000)]
    public string? AudioPath { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(ExamQuestionId))]
    public ExamQuestion? ExamQuestion { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public ExamCandidate? Candidate { get; set; }

    [ForeignKey(nameof(SelectedOptionId))]
    public QuestionOption? SelectedOption { get; set; }

    public AiEvaluation? AiEvaluation { get; set; }
    public ICollection<FollowUpQuestion> FollowUps { get; set; } = [];
}
