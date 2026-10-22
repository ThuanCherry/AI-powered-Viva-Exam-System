using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("rubric_criteria")]
public class RubricCriteria
{
    [Key]
    [Column("criteria_id")]
    public int CriteriaId { get; set; }

    [Column("rubric_id")]
    public int RubricId { get; set; }

    [Column("criteria_name")]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column("max_score")]
    public decimal MaxScore { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 1;

    [ForeignKey(nameof(RubricId))]
    public Rubric? Rubric { get; set; }
}
