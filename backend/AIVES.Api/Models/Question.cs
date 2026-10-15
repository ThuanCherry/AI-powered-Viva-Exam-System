using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("questions")]
public class Question
{
    [Key]
    [Column("question_id")]
    public int QuestionId { get; set; }

    [Column("course_id")]
    public int CourseId { get; set; }

    [Column("source_material_id")]
    public int? SourceMaterialId { get; set; }

    [Column("created_by")]
    public int CreatedBy { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [Column("expected_answer")]
    public string? ExpectedAnswer { get; set; }

    [Column("bloom_level")]
    [MaxLength(20)]
    public string BloomLevel { get; set; } = "Remember";

    [Column("difficulty")]
    [MaxLength(10)]
    public string Difficulty { get; set; } = "Easy";

    [Column("question_type")]
    [MaxLength(10)]
    public string QuestionType { get; set; } = "Main";

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [Column("source")]
    [MaxLength(10)]
    public string Source { get; set; } = "Manual";

    [Column("reviewed_by")]
    public int? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [ForeignKey(nameof(SourceMaterialId))]
    public CourseMaterial? SourceMaterial { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User? Creator { get; set; }

    [ForeignKey(nameof(ReviewedBy))]
    public User? Reviewer { get; set; }

    public ICollection<QuestionOption> Options { get; set; } = [];
}
