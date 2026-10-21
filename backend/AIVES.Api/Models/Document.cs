using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("documents")]
public class Document
{
    [Key]
    [Column("document_id")]
    public int DocumentId { get; set; }

    [Column("course_id")]
    public int CourseId { get; set; }

    [Column("uploaded_by_id")]
    public int UploadedById { get; set; }

    [Column("file_name")]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Column("content_type")]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("storage_path")]
    [MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("error_message")]
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public User? UploadedBy { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}

[Table("document_chunks")]
public class DocumentChunk
{
    [Key]
    [Column("chunk_id")]
    public int ChunkId { get; set; }

    [Column("document_id")]
    public int DocumentId { get; set; }

    [Column("chunk_index")]
    public int ChunkIndex { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("token_count")]
    public int TokenCount { get; set; }

    [Column("embedding_json")]
    public string EmbeddingJson { get; set; } = string.Empty;

    [ForeignKey(nameof(DocumentId))]
    public Document? Document { get; set; }
}

[Table("generated_questions")]
public class GeneratedQuestion
{
    [Key]
    [Column("question_id")]
    public int QuestionId { get; set; }

    [Column("document_id")]
    public int DocumentId { get; set; }

    [Column("course_id")]
    public int? CourseId { get; set; }

    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [Column("answer_key")]
    [MaxLength(2000)]
    public string? AnswerKey { get; set; }

    [Column("explanation")]
    [MaxLength(2000)]
    public string? Explanation { get; set; }

    [Column("bloom_level")]
    public int BloomLevel { get; set; }

    [Column("topic")]
    [MaxLength(200)]
    public string? Topic { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("reviewed_by_id")]
    public int? ReviewedById { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("review_notes")]
    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(DocumentId))]
    public Document? Document { get; set; }

    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public User? ReviewedBy { get; set; }
}
