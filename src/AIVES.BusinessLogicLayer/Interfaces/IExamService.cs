using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;

namespace AIVES.BusinessLogicLayer.Interfaces;

public interface IExamService
{
    Task<ServiceResult<Exam>> GetByIdAsync(int examId);
    Task<ServiceResult<IEnumerable<Exam>>> GetAllAsync();
    Task<ServiceResult<IEnumerable<Exam>>> GetBySubjectAsync(string subject);
    Task<ServiceResult<Exam>> CreateAsync(Exam exam, IEnumerable<Question> questions, int createdByUserId);
    Task<ServiceResult<Exam>> UpdateAsync(int examId, Exam updatedExam);
    Task<ServiceResult<bool>> DeleteAsync(int examId);
}
