using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AIVES.DataAccessLayer.Models;
[Table("questions")]
public class Question
{
    [Key, Column("id")] public long QuestionId { get; set; }
    [Column("course_id")] public long CourseId { get; set; }
    [Column("content")] public string QuestionText { get; set; } = "";
    [Column("bloom_level"), MaxLength(30)] public string? BloomLevel { get; set; }
    [Column("difficulty"), MaxLength(30)] public string? Difficulty { get; set; }
    [Column("is_approved")] public bool IsApproved { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}