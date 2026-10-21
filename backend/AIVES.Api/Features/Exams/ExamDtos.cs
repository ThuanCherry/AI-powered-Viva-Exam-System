namespace AIVES.Api.Features.Exams;

public record CreateExamRequest(
    int CourseId,
    string ExamName,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    int TimePerStudent,
    int MainQuestionCount = 3,
    int MaxFollowUpCount = 2,
    int SecondsPerQuestion = 120,
    int? LecturerId = null
);

public record CandidateScheduleItem(
    int StudentId,
    DateTime ScheduledTime
);

public record AssignCandidatesRequest(
    List<CandidateScheduleItem> Candidates
);

public record ConfigureQuestionsRequest(
    List<int>? SelectedQuestionIds,
    string Strategy = "RANDOM", // "RANDOM" | "ADAPTIVE"
    int? MainQuestionCount = null,
    int? MaxFollowUpCount = null
);

public record UpdateExamStatusRequest(
    string Status
);

public record ExamDetailDto(
    int ExamId,
    string ExamName,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    int TimePerStudent,
    int MainQuestionCount,
    int MaxFollowUpCount,
    string Status,
    DateTime CreatedAt,
    string? QuestionScopeJson,
    CourseSummaryDto? Course,
    LecturerSummaryDto? Lecturer,
    VivaSettingsDto? VivaSettings,
    List<CandidateDto> Candidates,
    int AssignedQuestionsCount
);

public record CourseSummaryDto(int CourseId, string CourseCode, string CourseName);
public record LecturerSummaryDto(int UserId, string FullName, string Email);
public record VivaSettingsDto(int SecondsPerQuestion, int MaxFollowUpsPerQuestion);
public record CandidateDto(
    int CandidateId,
    int StudentId,
    string? StudentName,
    string? StudentEmail,
    DateTime? ScheduledTime,
    string Status,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    List<AssignedQuestionDto>? Questions = null
);

public record AssignedQuestionDto(
    int ExamQuestionId,
    int QuestionId,
    string QuestionText,
    string Difficulty,
    string BloomLevel,
    int OrderNo
);
