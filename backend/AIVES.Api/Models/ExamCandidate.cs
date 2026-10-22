using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("exam_candidates")]
public class ExamCandidate
{
    [Key]
    [Column("candidate_id")]
    public int CandidateId { get; set; }

    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("student_id")]
    public int StudentId { get; set; }

    [Column("scheduled_time")]
    public DateTime? ScheduledTime { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Waiting";

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(ExamId))]
    public ExamSession? ExamSession { get; set; }

    [ForeignKey(nameof(StudentId))]
    public User? Student { get; set; }
}
