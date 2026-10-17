using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Common;
public record CourseCommand(string Code, string Name, string? Description, bool IsActive);
public record QuestionCommand(string Content, string? Difficulty, string? BloomLevel, bool IsApproved, bool IsActive);
public record CourseDetails(Course Course, IReadOnlyList<Question> Questions, IReadOnlyList<User> Students, IReadOnlyList<long> EnrolledStudentIds, IReadOnlySet<long> LockedQuestionIds);
