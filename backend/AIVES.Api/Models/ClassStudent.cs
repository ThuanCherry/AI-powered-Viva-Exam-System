using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Models;

[Table("class_students")]
[PrimaryKey(nameof(ClassId), nameof(StudentId))]
public class ClassStudent
{
    [Column("class_id")]
    public int ClassId { get; set; }

    [Column("student_id")]
    public int StudentId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(ClassId))]
    public AcademicClass? AcademicClass { get; set; }

    [ForeignKey(nameof(StudentId))]
    public User? Student { get; set; }
}
