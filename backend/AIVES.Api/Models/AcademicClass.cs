using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("academic_classes")]
public class AcademicClass
{
    [Key]
    [Column("class_id")]
    public int ClassId { get; set; }

    [Column("class_code")]
    [MaxLength(50)]
    public string ClassCode { get; set; } = string.Empty;

    [Column("class_name")]
    [MaxLength(200)]
    public string ClassName { get; set; } = string.Empty;

    [Column("course_id")]
    public int CourseId { get; set; }

    [Column("lecturer_id")]
    public int LecturerId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [ForeignKey(nameof(LecturerId))]
    public User? Lecturer { get; set; }
}
