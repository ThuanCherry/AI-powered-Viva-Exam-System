using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Common;
public record AuthUser(long UserId, string FullName, string Email, IReadOnlyList<string> Roles);
public record RegisterRequest(string FullName, string Email, string? StudentCode, string Password);
public record ExamCommand(long CourseId, string Title, string? Description, DateTime StartsAt, DateTime EndsAt,
    int DurationMinutesPerStudent, int MainQuestionCount, int MaxFollowUpPerQuestion, string SelectionStrategy, int AvoidRecentDuplicateCount);
public record SlotCommand(long ParticipantId, DateTime Start, DateTime End);
public record ExamSummary(Exam Exam, string CourseName);
public record ParticipantView(ExamParticipant Participant, string StudentName, string Email);
public record AssignmentView(long ParticipantId, int SequenceNo, Question Question, bool RecentDuplicate);
public record ExamDetails(Exam Exam, Course Course, IReadOnlyList<User> EligibleStudents,
    IReadOnlyList<ParticipantView> Participants, IReadOnlyList<Question> Questions,
    IReadOnlyList<long> SelectedQuestionIds, IReadOnlyList<AssignmentView> Assignments);
public record StudentSchedule(long ParticipantId, string ExamTitle, string CourseName, DateTime Start, DateTime End,
    int DurationMinutes, string Status);
