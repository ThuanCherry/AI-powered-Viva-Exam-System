using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("exam_questions")]
public class ExamQuestion
{
    [Key]
    [Column("exam_question_id")]
    public int ExamQuestionId { get; set; }

    [Column("exam_id")]
    public int ExamId { get; set; }

    [Column("candidate_id")]
    public int CandidateId { get; set; }

    [Column("question_id")]
    public int QuestionId { get; set; }

    [Column("parent_exam_question_id")]
    public int? ParentExamQuestionId { get; set; }

    [Column("order_no")]
    public int OrderNo { get; set; }

    [Column("asked_at")]
    public DateTime? AskedAt { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    // Navigation
    [ForeignKey(nameof(ExamId))]
    public ExamSession? ExamSession { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public ExamCandidate? Candidate { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public Question? Question { get; set; }

    [ForeignKey(nameof(ParentExamQuestionId))]
    public ExamQuestion? ParentExamQuestion { get; set; }

    public Answer? Answer { get; set; }
}
