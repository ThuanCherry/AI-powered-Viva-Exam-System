using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.DataAccessLayer.Models;

[Table("StudentAnswers")]
public class StudentAnswer
{
    [Key]
    public int AnswerId { get; set; }

    [Required]
    public string AnswerText { get; set; } = string.Empty;

    public string? AudioFilePath { get; set; }

    public double? Score { get; set; }

    public string? AIEvaluation { get; set; }

    public string? AIFeedback { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    // Foreign Keys
    public int SessionId { get; set; }

    [ForeignKey("SessionId")]
    public ExamSession ExamSession { get; set; } = null!;

    public int QuestionId { get; set; }

    [ForeignKey("QuestionId")]
    public Question Question { get; set; } = null!;
}
