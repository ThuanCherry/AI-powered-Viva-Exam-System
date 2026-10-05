using AIVES.DataAccessLayer.Models;
namespace AIVES.DataAccessLayer.Repositories;
public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }
    IGenericRepository<Role> Roles { get; }
    IGenericRepository<UserRole> UserRoles { get; }
    IGenericRepository<Course> Courses { get; }
    IGenericRepository<CourseLecturer> CourseLecturers { get; }
    IGenericRepository<CourseStudent> CourseStudents { get; }
    IGenericRepository<Exam> Exams { get; }
    IGenericRepository<Question> Questions { get; }
    IGenericRepository<ExamParticipant> Participants { get; }
    IGenericRepository<ExamQuestionPool> QuestionPool { get; }
    IGenericRepository<ExamQuestionAssignment> Assignments { get; }
    Task<int> SaveChangesAsync();
    Task<T> InTransactionAsync<T>(long? examId, Func<Task<T>> action, Func<T, bool> commit, long? courseId = null);
}
