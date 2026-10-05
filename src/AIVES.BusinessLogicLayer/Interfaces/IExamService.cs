using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Interfaces;
public interface IExamService
{
    Task<IReadOnlyList<ExamSummary>> GetAllAsync(long lecturerId);
    Task<ServiceResult<ExamDetails>> GetDetailsAsync(long examId, long lecturerId);
    Task<ServiceResult<Exam>> GetOwnedAsync(long examId, long lecturerId, bool draftOnly = false);
    Task<ServiceResult<Exam>> CreateAsync(ExamCommand command, long lecturerId);
    Task<ServiceResult<Exam>> UpdateAsync(long examId, ExamCommand command, long lecturerId);
    Task<ServiceResult<bool>> DeleteAsync(long examId, long lecturerId);
    Task<ServiceResult<bool>> SetParticipantsAsync(long examId, IReadOnlyList<long> studentIds, long lecturerId);
    Task<ServiceResult<bool>> SetQuestionPoolAsync(long examId, IReadOnlyList<long> questionIds, long lecturerId);
    Task<ServiceResult<bool>> PublishAsync(long examId, long lecturerId);
}
