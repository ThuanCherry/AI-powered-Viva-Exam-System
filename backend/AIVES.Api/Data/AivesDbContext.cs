using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Data;

public class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<AcademicClass> AcademicClasses => Set<AcademicClass>();
    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<ExamVivaSettings> ExamVivaSettings => Set<ExamVivaSettings>();
    public DbSet<ExamCandidate> ExamCandidates => Set<ExamCandidate>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<AiEvaluation> AiEvaluations => Set<AiEvaluation>();
    public DbSet<FollowUpQuestion> FollowUpQuestions => Set<FollowUpQuestion>();
    public DbSet<ExamRecording> ExamRecordings => Set<ExamRecording>();
    public DbSet<ExamLog> ExamLogs => Set<ExamLog>();
    public DbSet<FinalResult> FinalResults => Set<FinalResult>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriteria> RubricCriteria => Set<RubricCriteria>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<GeneratedQuestion> GeneratedQuestions => Set<GeneratedQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite key for class_students
        modelBuilder.Entity<ClassStudent>()
            .HasKey(cs => new { cs.ClassId, cs.StudentId });

        // ExamVivaSettings shares PK with ExamSession (1:1)
        modelBuilder.Entity<ExamSession>()
            .HasOne(e => e.VivaSettings)
            .WithOne(v => v!.ExamSession!)
            .HasForeignKey<ExamVivaSettings>(v => v.ExamId);

        // Answer -> ExamQuestion (1:1 via unique constraint on exam_question_id)
        modelBuilder.Entity<ExamQuestion>()
            .HasOne(eq => eq.Answer)
            .WithOne(a => a!.ExamQuestion!)
            .HasForeignKey<Answer>(a => a.ExamQuestionId);

        // AiEvaluation -> Answer (1:1 via unique constraint on answer_id)
        modelBuilder.Entity<Answer>()
            .HasOne(a => a.AiEvaluation)
            .WithOne(e => e!.Answer!)
            .HasForeignKey<AiEvaluation>(e => e.AnswerId);

        // Rubric -> Question (1:1 via unique constraint on question_id)
        modelBuilder.Entity<Question>()
            .HasOne<Rubric>()
            .WithOne(r => r!.Question!)
            .HasForeignKey<Rubric>(r => r.QuestionId);

        // Self-referencing FK for exam_questions (follow-up parent)
        modelBuilder.Entity<ExamQuestion>()
            .HasOne(eq => eq.ParentExamQuestion)
            .WithMany()
            .HasForeignKey(eq => eq.ParentExamQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index: answers.exam_question_id
        modelBuilder.Entity<Answer>()
            .HasIndex(a => a.ExamQuestionId)
            .IsUnique();

        // Unique index: exam_candidates(exam_id, student_id)
        modelBuilder.Entity<ExamCandidate>()
            .HasIndex(ec => new { ec.ExamId, ec.StudentId })
            .IsUnique();

        // Unique index: final_results(exam_id, candidate_id)
        modelBuilder.Entity<FinalResult>()
            .HasIndex(fr => new { fr.ExamId, fr.CandidateId })
            .IsUnique();
    }
}
