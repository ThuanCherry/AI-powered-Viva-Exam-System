using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace AIVES.DataAccessLayer.Models;
[Table("roles")]
public class Role
{
    [Key, Column("id")] public long RoleId { get; set; }
    [Column("code"), MaxLength(50)] public string Code { get; set; } = "";
    [Column("name"), MaxLength(100)] public string Name { get; set; } = "";
    [Column("description"), MaxLength(255)] public string? Description { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
[Table("user_roles")]
public class UserRole
{
    [Column("user_id")] public long UserId { get; set; }
    [Column("role_id")] public long RoleId { get; set; }
    [Column("assigned_at")] public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
[Table("course_lecturers")]
public class CourseLecturer
{
    [Column("course_id")] public long CourseId { get; set; }
    [Column("lecturer_id")] public long LecturerId { get; set; }
    [Column("assigned_at")] public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
[Table("course_students")]
public class CourseStudent
{
    [Column("course_id")] public long CourseId { get; set; }
    [Column("student_id")] public long StudentId { get; set; }
    [Column("enrolled_at")] public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    [Column("is_active")] public bool IsActive { get; set; } = true;
}
[Table("exam_participants")]
public class ExamParticipant
{
    [Key, Column("id")] public long ParticipantId { get; set; }
    [Column("exam_id")] public long ExamId { get; set; }
    [Column("student_id")] public long StudentId { get; set; }
    [Column("scheduled_start_at")] public DateTime ScheduledStartAt { get; set; }
    [Column("scheduled_end_at")] public DateTime ScheduledEndAt { get; set; }
    [Column("slot_order")] public int SlotOrder { get; set; }
    [Column("status")] public string Status { get; set; } = "SCHEDULED";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
[Table("exam_question_pool")]
public class ExamQuestionPool
{
    [Column("exam_id")] public long ExamId { get; set; }
    [Column("question_id")] public long QuestionId { get; set; }
    [Column("weight")] public decimal Weight { get; set; } = 1;
    [Column("is_enabled")] public bool IsEnabled { get; set; } = true;
    [Column("added_at")] public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
[Table("exam_question_assignments")]
public class ExamQuestionAssignment
{
    [Key, Column("id")] public long AssignmentId { get; set; }
    [Column("exam_participant_id")] public long ParticipantId { get; set; }
    [Column("question_id")] public long QuestionId { get; set; }
    [Column("sequence_no")] public int SequenceNo { get; set; }
    [Column("assignment_status")] public string AssignmentStatus { get; set; } = "RESERVED";
    [Column("assigned_at")] public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}