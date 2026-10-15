using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("rubrics")]
public class Rubric
{
    [Key]
    [Column("rubric_id")]
    public int RubricId { get; set; }

    [Column("question_id")]
    public int QuestionId { get; set; }

    [Column("total_score")]
    public decimal TotalScore { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public Question? Question { get; set; }

    public ICollection<RubricCriteria> Criteria { get; set; } = [];
}
