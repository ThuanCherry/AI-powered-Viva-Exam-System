using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("exam_recordings")]
public class ExamRecording
{
    [Key]
    [Column("recording_id")]
    public int RecordingId { get; set; }

    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("candidate_id")]
    public int CandidateId { get; set; }

    [Column("file_path")]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Column("file_type")]
    [MaxLength(10)]
    public string FileType { get; set; } = string.Empty;

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(ExamId))]
    public ExamSession? ExamSession { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public ExamCandidate? Candidate { get; set; }
}
