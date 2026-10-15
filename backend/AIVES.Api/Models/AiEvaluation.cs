using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("ai_evaluations")]
public class AiEvaluation
{
    [Key]
    [Column("evaluation_id")]
    public int EvaluationId { get; set; }

    [Column("answer_id")]
    public int AnswerId { get; set; }

    [Column("ai_score")]
    public decimal AiScore { get; set; }

    [Column("confidence")]
    public decimal? Confidence { get; set; }

    [Column("feedback")]
    public string? Feedback { get; set; }

    [Column("strengths")]
    public string? Strengths { get; set; }

    [Column("weaknesses")]
    public string? Weaknesses { get; set; }

    [Column("missing_points")]
    public string? MissingPoints { get; set; }

    [Column("evaluated_at")]
    public DateTime EvaluatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(AnswerId))]
    public Answer? Answer { get; set; }
}
