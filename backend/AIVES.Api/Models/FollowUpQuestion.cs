using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("follow_up_questions")]
public class FollowUpQuestion
{
    [Key]
    [Column("follow_up_id")]
    public int FollowUpId { get; set; }

    [Column("answer_id")]
    public int AnswerId { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [Column("answer_transcript")]
    public string? AnswerTranscript { get; set; }

    [Column("audio_path")]
    [MaxLength(1000)]
    public string? AudioPath { get; set; }

    [Column("reason")]
    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Column("order_no")]
    public int OrderNo { get; set; } = 1;

    [Column("asked_at")]
    public DateTime? AskedAt { get; set; }

    [Column("answered_at")]
    public DateTime? AnsweredAt { get; set; }

    // Navigation
    [ForeignKey(nameof(AnswerId))]
    public Answer? Answer { get; set; }
}
