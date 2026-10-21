using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVES.Api.Models;

[Table("course_materials")]
public class CourseMaterial
{
    [Key]
    [Column("material_id")]
    public int MaterialId { get; set; }

    [Column("course_id")]
    public int CourseId { get; set; }

    [Column("file_name")]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Column("file_path")]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    [Column("file_type")]
    [MaxLength(10)]
    public string FileType { get; set; } = string.Empty;

    [Column("file_size")]
    public long? FileSize { get; set; }

    [Column("uploaded_by")]
    public int UploadedBy { get; set; }

    [Column("processing_status")]
    [MaxLength(20)]
    public string ProcessingStatus { get; set; } = "Pending";

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(CourseId))]
    public Course? Course { get; set; }

    [ForeignKey(nameof(UploadedBy))]
    public User? Uploader { get; set; }
}
