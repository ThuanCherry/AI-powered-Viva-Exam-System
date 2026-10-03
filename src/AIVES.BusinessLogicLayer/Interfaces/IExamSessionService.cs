using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;

namespace AIVES.BusinessLogicLayer.Interfaces;

public interface IExamSessionService
{
    Task<ServiceResult<ExamSession>> GetByIdAsync(int sessionId);
    Task<ServiceResult<IEnumerable<ExamSession>>> GetByStudentAsync(int studentUserId);
    Task<ServiceResult<ExamSession>> StartSessionAsync(int examId, int studentUserId);
    Task<ServiceResult<StudentAnswer>> SubmitAnswerAsync(int sessionId, int questionId, string answerText, string? audioFilePath = null);
    Task<ServiceResult<ExamSession>> CompleteSessionAsync(int sessionId);
    Task<ServiceResult<IEnumerable<ExamSession>>> GetResultsByExamAsync(int examId);
}
