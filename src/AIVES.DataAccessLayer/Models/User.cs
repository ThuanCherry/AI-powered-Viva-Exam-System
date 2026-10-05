using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace AIVES.DataAccessLayer.Models;
[Table("users")]
public class User
{
    [Key, Column("id")] public long UserId { get; set; }
    [Column("email"), MaxLength(255)] public string Email { get; set; } = "";
    [Column("full_name"), MaxLength(255)] public string FullName { get; set; } = "";
    [JsonIgnore, Column("password_hash"), MaxLength(500)] public string PasswordHash { get; set; } = "";
    [Column("student_code"), MaxLength(50)] public string? StudentCode { get; set; }
    [Column("lecturer_code"), MaxLength(50)] public string? LecturerCode { get; set; }
    [Column("status")] public string Status { get; set; } = "ACTIVE";
    [Column("email_verified_at")] public DateTime? EmailVerifiedAt { get; set; }
    [Column("failed_login_count")] public int FailedLoginCount { get; set; }
    [Column("lockout_end_at")] public DateTime? LockoutEndAt { get; set; }
    [Column("last_login_at")] public DateTime? LastLoginAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}