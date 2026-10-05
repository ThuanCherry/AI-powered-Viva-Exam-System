using AIVES.DataAccessLayer.Data;
using AIVES.DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
namespace AIVES.DataAccessLayer.Repositories;
public class UnitOfWork(AivesDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();
    private IGenericRepository<T> Repository<T>() where T : class
    {
        if (!_repositories.TryGetValue(typeof(T), out var repository))
            _repositories[typeof(T)] = repository = new GenericRepository<T>(context);
        return (IGenericRepository<T>)repository;
    }
    public IGenericRepository<User> Users => Repository<User>();
    public IGenericRepository<Role> Roles => Repository<Role>();
    public IGenericRepository<UserRole> UserRoles => Repository<UserRole>();
    public IGenericRepository<Course> Courses => Repository<Course>();
    public IGenericRepository<CourseLecturer> CourseLecturers => Repository<CourseLecturer>();
    public IGenericRepository<CourseStudent> CourseStudents => Repository<CourseStudent>();
    public IGenericRepository<Exam> Exams => Repository<Exam>();
    public IGenericRepository<Question> Questions => Repository<Question>();
    public IGenericRepository<ExamParticipant> Participants => Repository<ExamParticipant>();
    public IGenericRepository<ExamQuestionPool> QuestionPool => Repository<ExamQuestionPool>();
    public IGenericRepository<ExamQuestionAssignment> Assignments => Repository<ExamQuestionAssignment>();
    public Task<int> SaveChangesAsync() => context.SaveChangesAsync();
    public async Task<T> InTransactionAsync<T>(long? examId, Func<Task<T>> action, Func<T, bool> commit, long? courseId = null)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            if (examId.HasValue)
                courseId = await context.Exams.AsNoTracking().Where(x => x.ExamId == examId.Value).Select(x => (long?)x.CourseId).SingleOrDefaultAsync();
            if (courseId.HasValue)
            {
                await context.Courses.FromSqlInterpolated($"SELECT * FROM courses WHERE CourseId = {courseId.Value} FOR UPDATE").AsNoTracking().ToListAsync();
                context.ChangeTracker.Clear();
            }
            // Serializes every mutation of one exam, including publish vs regeneration.
            if (examId.HasValue)
            {
                await context.Exams.FromSqlInterpolated($"SELECT * FROM exams WHERE ExamId = {examId.Value} FOR UPDATE").AsNoTracking().ToListAsync();
                context.ChangeTracker.Clear();
            }
            var result = await action();
            if (commit(result)) await transaction.CommitAsync();
            else { await transaction.RollbackAsync(); context.ChangeTracker.Clear(); }
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            context.ChangeTracker.Clear();
            throw;
        }
    }
    public void Dispose() { context.Dispose(); GC.SuppressFinalize(this); }
}
