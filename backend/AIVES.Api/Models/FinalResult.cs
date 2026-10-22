using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("final_results")]
public class FinalResult
{
    [Key]
    [Column("result_id")]
    public int ResultId { get; set; }

    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("candidate_id")]
    public int CandidateId { get; set; }

    [Column("ai_score")]
    public decimal? AiScore { get; set; }

    [Column("lecturer_score")]
    public decimal? LecturerScore { get; set; }

    [Column("final_score")]
    public decimal? FinalScore { get; set; }

    [Column("lecturer_comment")]
    public string? LecturerComment { get; set; }

    [Column("finalized_by")]
    public int? FinalizedBy { get; set; }

    [Column("finalized_at")]
    public DateTime? FinalizedAt { get; set; }

    [ForeignKey(nameof(ExamId))]
    public ExamSession? ExamSession { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public ExamCandidate? Candidate { get; set; }

    [ForeignKey(nameof(FinalizedBy))]
    public User? FinalizedByUser { get; set; }
}
