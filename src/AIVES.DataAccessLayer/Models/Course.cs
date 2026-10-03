using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.DataAccessLayer.Models;

[Table("Courses")]
public class Course
{
    [Key]
    public int CourseId { get; set; }

    [Required, MaxLength(100)]
    public string CourseCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CourseName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int Credits { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
