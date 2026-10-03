using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.DataAccessLayer.Models;

[Table("Exams")]
public class Exam
{
    [Key]
    public int ExamId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string Subject { get; set; } = string.Empty;

    public int DurationMinutes { get; set; } = 30;

    public int TotalQuestions { get; set; } = 10;

    [MaxLength(20)]
    public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard

    public int? CourseId { get; set; }

    [ForeignKey("CourseId")]
    public Course? Course { get; set; }

    public bool IsAIGenerated { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Foreign Key
    public int CreatedByUserId { get; set; }

    [ForeignKey("CreatedByUserId")]
    public User CreatedBy { get; set; } = null!;

    // Navigation properties
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();
}
