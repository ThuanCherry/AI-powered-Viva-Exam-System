using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("question_options")]
public class QuestionOption
{
    [Key]
    [Column("option_id")]
    public int OptionId { get; set; }

    [Column("question_id")]
    public int QuestionId { get; set; }

    [Column("option_text")]
    public string OptionText { get; set; } = string.Empty;

    [Column("is_correct")]
    public bool IsCorrect { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 1;

    // Navigation
    [ForeignKey(nameof(QuestionId))]
    public Question? Question { get; set; }
}
