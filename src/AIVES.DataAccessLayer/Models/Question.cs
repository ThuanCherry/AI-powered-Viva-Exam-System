using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.DataAccessLayer.Models;

[Table("Questions")]
public class Question
{
    [Key]
    public int QuestionId { get; set; }

    [Required]
    public string QuestionText { get; set; } = string.Empty;

    public string? ExpectedAnswer { get; set; }

    public string? AIPromptContext { get; set; }

    [MaxLength(20)]
    public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard

    public int MaxScore { get; set; } = 10;

    public int OrderIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Key
    public int ExamId { get; set; }

    [ForeignKey("ExamId")]
    public Exam Exam { get; set; } = null!;

    // Navigation properties
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}
