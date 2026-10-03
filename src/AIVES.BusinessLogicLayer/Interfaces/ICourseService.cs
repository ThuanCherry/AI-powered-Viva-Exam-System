using AIVES.BusinessLogicLayer.Common;
using AIVES.DataAccessLayer.Models;

namespace AIVES.BusinessLogicLayer.Interfaces;

public interface ICourseService
{
    Task<ServiceResult<Course>> GetByIdAsync(int courseId);
    Task<ServiceResult<IEnumerable<Course>>> GetAllAsync();
    Task<ServiceResult<Course>> CreateAsync(Course course);
    Task<ServiceResult<bool>> DeleteAsync(int courseId);
}
