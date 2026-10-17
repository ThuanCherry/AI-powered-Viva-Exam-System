using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Interfaces;
public interface ICourseService
{
    Task<IReadOnlyList<Course>> GetForLecturerAsync(long lecturerId, bool includeInactive = false);
    Task<ServiceResult<CourseDetails>> GetDetailsAsync(long courseId, long lecturerId);
    Task<ServiceResult<Course>> CreateAsync(CourseCommand command, long lecturerId);
    Task<ServiceResult<Course>> UpdateAsync(long courseId, CourseCommand command, long lecturerId);
    Task<ServiceResult<bool>> SetStudentsAsync(long courseId, IReadOnlyList<long> studentIds, long lecturerId);
    Task<ServiceResult<Question>> SaveQuestionAsync(long courseId, long? questionId, QuestionCommand command, long lecturerId);
}