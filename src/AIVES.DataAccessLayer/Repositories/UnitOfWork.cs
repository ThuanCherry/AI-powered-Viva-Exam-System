using AIVES.DataAccessLayer.Data;
using AIVES.DataAccessLayer.Models;

namespace AIVES.DataAccessLayer.Repositories;

/// <summary>
/// Unit of Work implementation that coordinates multiple repositories
/// and manages database transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AivesDbContext _context;

    private IGenericRepository<User>? _users;
    private IGenericRepository<Course>? _courses;
    private IGenericRepository<Exam>? _exams;
    private IGenericRepository<Question>? _questions;
    private IGenericRepository<ExamSession>? _examSessions;
    private IGenericRepository<StudentAnswer>? _studentAnswers;

    public UnitOfWork(AivesDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<User> Users =>
        _users ??= new GenericRepository<User>(_context);

    public IGenericRepository<Course> Courses =>
        _courses ??= new GenericRepository<Course>(_context);

    public IGenericRepository<Exam> Exams =>
        _exams ??= new GenericRepository<Exam>(_context);

    public IGenericRepository<Question> Questions =>
        _questions ??= new GenericRepository<Question>(_context);

    public IGenericRepository<ExamSession> ExamSessions =>
        _examSessions ??= new GenericRepository<ExamSession>(_context);

    public IGenericRepository<StudentAnswer> StudentAnswers =>
        _studentAnswers ??= new GenericRepository<StudentAnswer>(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
