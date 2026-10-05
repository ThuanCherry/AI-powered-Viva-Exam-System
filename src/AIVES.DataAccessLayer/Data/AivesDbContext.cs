using AIVES.DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
namespace AIVES.DataAccessLayer.Data;

/// <summary>Maps the existing MySQL schema in docs/AIVES_DB.txt; never creates or resets it.</summary>
public class AivesDbContext(DbContextOptions<AivesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseLecturer> CourseLecturers => Set<CourseLecturer>();
    public DbSet<CourseStudent> CourseStudents => Set<CourseStudent>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamParticipant> Participants => Set<ExamParticipant>();
    public DbSet<ExamQuestionPool> QuestionPool => Set<ExamQuestionPool>();
    public DbSet<ExamQuestionAssignment> Assignments => Set<ExamQuestionAssignment>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Ignore<ExamSession>();
        m.Ignore<StudentAnswer>();
        m.Entity<User>().HasIndex(x => x.Email).IsUnique();
        m.Entity<User>().HasIndex(x => x.StudentCode).IsUnique();
        m.Entity<User>().HasIndex(x => x.LecturerCode).IsUnique();
        m.Entity<Role>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Course>().HasIndex(x => x.CourseCode).IsUnique();
        m.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        m.Entity<CourseLecturer>().HasKey(x => new { x.CourseId, x.LecturerId });
        m.Entity<CourseStudent>().HasKey(x => new { x.CourseId, x.StudentId });
        m.Entity<ExamQuestionPool>().HasKey(x => new { x.ExamId, x.QuestionId });
        m.Entity<ExamQuestionPool>().Property(x => x.Weight).HasPrecision(6, 2);
        m.Entity<ExamParticipant>().HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
        m.Entity<ExamParticipant>().HasIndex(x => new { x.ExamId, x.SlotOrder }).IsUnique();
        m.Entity<ExamQuestionAssignment>().HasIndex(x => new { x.ParticipantId, x.SequenceNo }).IsUnique();
        m.Entity<ExamQuestionAssignment>().HasIndex(x => new { x.ParticipantId, x.QuestionId }).IsUnique();
        m.Entity<UserRole>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<UserRole>().HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CourseLecturer>().HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CourseLecturer>().HasOne<User>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CourseStudent>().HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CourseStudent>().HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Exam>().HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Exam>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Question>().HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ExamParticipant>().HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ExamParticipant>().HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ExamQuestionPool>().HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ExamQuestionPool>().HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ExamQuestionAssignment>().HasOne<ExamParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ExamQuestionAssignment>().HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}