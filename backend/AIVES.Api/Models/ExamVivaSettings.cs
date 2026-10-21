using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("exam_viva_settings")]
public class ExamVivaSettings
{
    [Key]
    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("seconds_per_question")]
    public int SecondsPerQuestion { get; set; } = 120;

    [Column("max_follow_ups_per_question")]
    public int MaxFollowUpsPerQuestion { get; set; } = 1;

    // Navigation
    [ForeignKey(nameof(ExamId))]
    public ExamSession? ExamSession { get; set; }
}
