using AIVES.DataAccessLayer.Models;

namespace AIVES.DataAccessLayer.Repositories;

/// <summary>
/// Unit of Work interface that manages all repositories and database transactions.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }
    IGenericRepository<Course> Courses { get; }
    IGenericRepository<Exam> Exams { get; }
    IGenericRepository<Question> Questions { get; }
    IGenericRepository<ExamSession> ExamSessions { get; }
    IGenericRepository<StudentAnswer> StudentAnswers { get; }

    Task<int> SaveChangesAsync();
}
