using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AIVES.DataAccessLayer.Models;
[Table("courses")]
public class Course
{
    [Key, Column("CourseId")] public long CourseId { get; set; }
    [Column("code"), MaxLength(50)] public string CourseCode { get; set; } = "";
    [Column("name"), MaxLength(255)] public string CourseName { get; set; } = "";
    [Column("description")] public string? Description { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}