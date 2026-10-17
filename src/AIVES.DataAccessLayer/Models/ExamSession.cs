using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.DataAccessLayer.Models;

[Table("ExamSessions")]
public class ExamSession
{
    [Key]
    public int SessionId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Abandoned

    public double? TotalScore { get; set; }

    public double? MaxPossibleScore { get; set; }

    public string? AIFeedbackSummary { get; set; }

    // Foreign Keys
    public int StudentUserId { get; set; }

    [ForeignKey("StudentUserId")]
    public User Student { get; set; } = null!;

    public int ExamId { get; set; }

    [ForeignKey("ExamId")]
    public Exam Exam { get; set; } = null!;

    // Navigation properties
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}
