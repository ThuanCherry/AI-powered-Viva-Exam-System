using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.Extensions.Logging;

namespace AIVES.BusinessLogicLayer.Services;

public class ExamSessionService : IExamSessionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExamSessionService> _logger;

    public ExamSessionService(IUnitOfWork unitOfWork, ILogger<ExamSessionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<ExamSession>> GetByIdAsync(int sessionId)
    {
        var session = await _unitOfWork.ExamSessions.GetByIdAsync(sessionId);
        if (session == null)
            return ServiceResult<ExamSession>.FailureResult("Session not found.");

        return ServiceResult<ExamSession>.SuccessResult(session);
    }

    public async Task<ServiceResult<IEnumerable<ExamSession>>> GetByStudentAsync(int studentUserId)
    {
        var sessions = await _unitOfWork.ExamSessions.FindAsync(s => s.StudentUserId == studentUserId);
        return ServiceResult<IEnumerable<ExamSession>>.SuccessResult(sessions);
    }

    public async Task<ServiceResult<ExamSession>> StartSessionAsync(int examId, int studentUserId)
    {
        // Validate exam exists
        var exam = await _unitOfWork.Exams.GetByIdAsync(examId);
        if (exam == null)
            return ServiceResult<ExamSession>.FailureResult("Exam not found.");

        // Validate student exists
        var student = await _unitOfWork.Users.GetByIdAsync(studentUserId);
        if (student == null)
            return ServiceResult<ExamSession>.FailureResult("Student not found.");

        // Check for active sessions
        var activeSessions = await _unitOfWork.ExamSessions.FindAsync(
            s => s.StudentUserId == studentUserId && s.ExamId == examId && s.Status == "InProgress");

        if (activeSessions.Any())
            return ServiceResult<ExamSession>.FailureResult("Student already has an active session for this exam.");

        var session = new ExamSession
        {
            ExamId = examId,
            StudentUserId = studentUserId,
            StartTime = DateTime.UtcNow,
            Status = "InProgress"
        };

        await _unitOfWork.ExamSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Exam session started: Student {StudentId}, Exam {ExamId}", studentUserId, examId);
        return ServiceResult<ExamSession>.SuccessResult(session, "Exam session started successfully.");
    }

    public async Task<ServiceResult<StudentAnswer>> SubmitAnswerAsync(int sessionId, int questionId, string answerText, string? audioFilePath = null)
    {
        var session = await _unitOfWork.ExamSessions.GetByIdAsync(sessionId);
        if (session == null)
            return ServiceResult<StudentAnswer>.FailureResult("Session not found.");

        if (session.Status != "InProgress")
            return ServiceResult<StudentAnswer>.FailureResult("Session is not active.");

        var question = await _unitOfWork.Questions.GetByIdAsync(questionId);
        if (question == null)
            return ServiceResult<StudentAnswer>.FailureResult("Question not found.");

        // Chấm điểm câu trả lời trực tiếp trong Service
        var (score, evaluation, feedback) = EvaluateVivaAnswer(
            answerText,
            question.ExpectedAnswer,
            question.AIPromptContext,
            question.MaxScore);

        var answer = new StudentAnswer
        {
            SessionId = sessionId,
            QuestionId = questionId,
            AnswerText = answerText,
            AudioFilePath = audioFilePath,
            Score = score,
            AIEvaluation = evaluation,
            AIFeedback = feedback,
            AnsweredAt = DateTime.UtcNow
        };

        await _unitOfWork.StudentAnswers.AddAsync(answer);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Answer submitted: Session {SessionId}, Question {QuestionId}, Score: {Score}",
            sessionId, questionId, score);

        return ServiceResult<StudentAnswer>.SuccessResult(answer, "Answer submitted and evaluated.");
    }

    public async Task<ServiceResult<ExamSession>> CompleteSessionAsync(int sessionId)
    {
        var session = await _unitOfWork.ExamSessions.GetByIdAsync(sessionId);
        if (session == null)
            return ServiceResult<ExamSession>.FailureResult("Session not found.");

        if (session.Status != "InProgress")
            return ServiceResult<ExamSession>.FailureResult("Session is not active.");

        // Calculate total score
        var answers = await _unitOfWork.StudentAnswers.FindAsync(a => a.SessionId == sessionId);
        var totalScore = answers.Sum(a => a.Score ?? 0);

        var questions = await _unitOfWork.Questions.FindAsync(q => q.ExamId == session.ExamId);
        var maxPossibleScore = questions.Sum(q => q.MaxScore);

        session.EndTime = DateTime.UtcNow;
        session.Status = "Completed";
        session.TotalScore = totalScore;
        session.MaxPossibleScore = maxPossibleScore;
        session.AIFeedbackSummary = GenerateSessionFeedback(totalScore, maxPossibleScore);

        _unitOfWork.ExamSessions.Update(session);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Exam session completed: {SessionId}, Score: {Score}/{MaxScore}",
            sessionId, totalScore, maxPossibleScore);

        return ServiceResult<ExamSession>.SuccessResult(session, "Exam session completed.");
    }

    public async Task<ServiceResult<IEnumerable<ExamSession>>> GetResultsByExamAsync(int examId)
    {
        var sessions = await _unitOfWork.ExamSessions.FindAsync(
            s => s.ExamId == examId && s.Status == "Completed");

        return ServiceResult<IEnumerable<ExamSession>>.SuccessResult(sessions);
    }

    // ===== Scoring Logic & Helper Methods =====
    private static (double Score, string Evaluation, string Feedback) EvaluateVivaAnswer(
        string studentAnswer,
        string? expectedAnswer,
        string? aiContext,
        int maxScore)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer))
        {
            return (0, "No Answer", "Học viên không cung cấp câu trả lời.");
        }

        if (string.IsNullOrWhiteSpace(expectedAnswer))
        {
            return (Math.Round(maxScore * 0.7, 1), "Reviewed", "Câu trả lời đã được ghi nhận và cần giảng viên phúc khảo.");
        }

        var similarity = CalculateKeywordOverlap(studentAnswer, expectedAnswer);
        var calculatedScore = Math.Round(similarity * maxScore, 1);

        string evaluation;
        string feedback;

        var ratio = maxScore > 0 ? calculatedScore / maxScore : 0;
        if (ratio >= 0.8)
        {
            evaluation = "Excellent";
            feedback = "Câu trả lời xuất sắc, nắm rất vững khái niệm chuyên môn.";
        }
        else if (ratio >= 0.6)
        {
            evaluation = "Good";
            feedback = "Câu trả lời tốt, truyền đạt được ý chính nhưng cần bổ sung thêm chi tiết kỹ thuật.";
        }
        else if (ratio >= 0.4)
        {
            evaluation = "Average";
            feedback = "Câu trả lời ở mức trung bình, còn thiếu các luận điểm cốt lõi.";
        }
        else
        {
            evaluation = "Poor";
            feedback = "Câu trả lời chưa đạt yêu cầu, vui lòng ôn tập lại nội dung này.";
        }

        return (calculatedScore, evaluation, feedback);
    }

    private static double CalculateKeywordOverlap(string answer, string expected)
    {
        var wordsA = answer.ToLower().Split(new[] { ' ', ',', '.', ';', '?', '!' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = expected.ToLower().Split(new[] { ' ', ',', '.', ';', '?', '!' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (wordsB.Count == 0) return 0;

        var intersection = wordsA.Intersect(wordsB).Count();
        return (double)intersection / wordsB.Count;
    }

    private static string GenerateSessionFeedback(double totalScore, double maxScore)
    {
        var percentage = maxScore > 0 ? (totalScore / maxScore) * 100 : 0;
        return percentage switch
        {
            >= 90 => "Excellent performance! Outstanding understanding of the subject matter.",
            >= 75 => "Good performance. Strong grasp of most concepts with minor areas for improvement.",
            >= 60 => "Satisfactory performance. Adequate understanding but several areas need review.",
            >= 40 => "Below average. Significant gaps in understanding. Additional study recommended.",
            _ => "Poor performance. Fundamental concepts need to be reviewed thoroughly."
        };
    }
}
