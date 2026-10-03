using AIVES.DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DataAccessLayer.Data;

/// <summary>
/// AivesDbContext theo đúng sơ đồ kiến trúc:
/// Hỗ trợ EF Core kết nối MySQL 8.4 (Pomelo) hoặc SQL Server.
/// </summary>
public class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasMany(u => u.CreatedExams)
                  .WithOne(e => e.CreatedBy)
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(u => u.ExamSessions)
                  .WithOne(s => s.Student)
                  .HasForeignKey(s => s.StudentUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Course configuration
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasIndex(c => c.CourseCode).IsUnique();

            entity.HasMany(c => c.Exams)
                  .WithOne(e => e.Course)
                  .HasForeignKey(e => e.CourseId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Exam configuration
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasMany(e => e.Questions)
                  .WithOne(q => q.Exam)
                  .HasForeignKey(q => q.ExamId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ExamSessions)
                  .WithOne(s => s.Exam)
                  .HasForeignKey(s => s.ExamId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ExamSession configuration
        modelBuilder.Entity<ExamSession>(entity =>
        {
            entity.HasMany(s => s.StudentAnswers)
                  .WithOne(a => a.ExamSession)
                  .HasForeignKey(a => a.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // StudentAnswer configuration
        modelBuilder.Entity<StudentAnswer>(entity =>
        {
            entity.HasOne(a => a.Question)
                  .WithMany(q => q.StudentAnswers)
                  .HasForeignKey(a => a.QuestionId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // Seed default demo data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = 1,
                FullName = "Admin User",
                Email = "admin@aives.com",
                PasswordHash = "AQAAAAIAAYagAAAAEH+Y...",
                Role = "Admin",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            },
            new User
            {
                UserId = 2,
                FullName = "Teacher Demo",
                Email = "teacher@aives.com",
                PasswordHash = "AQAAAAIAAYagAAAAEH+Y...",
                Role = "Teacher",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            },
            new User
            {
                UserId = 3,
                FullName = "Student Demo",
                Email = "student@aives.com",
                PasswordHash = "AQAAAAIAAYagAAAAEH+Y...",
                Role = "Student",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            }
        );

        modelBuilder.Entity<Course>().HasData(
            new Course
            {
                CourseId = 1,
                CourseCode = "PRN212",
                CourseName = "Basic Cross-Platform Application Programming With .NET",
                Description = "C# programming, OOP, EF Core, and 3-Layer Architecture",
                Credits = 3,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Course
            {
                CourseId = 2,
                CourseCode = "SWD392",
                CourseName = "Software Architecture and Design",
                Description = "Software architectural styles, patterns and full-stack design",
                Credits = 3,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
