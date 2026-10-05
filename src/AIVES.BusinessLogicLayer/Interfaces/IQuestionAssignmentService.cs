using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Interfaces;
public interface IQuestionAssignmentService
{
    Task<ServiceResult<bool>> GenerateAsync(long examId, long lecturerId);
}
public interface IQuestionSelectionStrategy
{
    string Name { get; }
    IReadOnlyList<Question> Select(IReadOnlyList<Question> candidates, int count, IReadOnlySet<long> recent);
}