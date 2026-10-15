using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("exam_logs")]
public class ExamLog
{
    [Key]
    [Column("log_id")]
    public long LogId { get; set; }

    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("candidate_id")]
    public int? CandidateId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("action")]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [ForeignKey(nameof(ExamId))]
    public ExamSession? ExamSession { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public ExamCandidate? Candidate { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
