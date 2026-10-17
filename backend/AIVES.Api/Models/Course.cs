using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("courses")]
public class Course
{
    [Key]
    [Column("course_id")]
    public int CourseId { get; set; }

    [Column("course_code")]
    [MaxLength(50)]
    public string CourseCode { get; set; } = string.Empty;

    [Column("course_name")]
    [MaxLength(200)]
    public string CourseName { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column("lecturer_id")]
    public int LecturerId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(LecturerId))]
    public User? Lecturer { get; set; }
}
