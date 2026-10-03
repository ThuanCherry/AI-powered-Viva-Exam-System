using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.Extensions.Logging;

namespace AIVES.BusinessLogicLayer.Services;

public class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CourseService> _logger;

    public CourseService(IUnitOfWork unitOfWork, ILogger<CourseService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<Course>> GetByIdAsync(int courseId)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(courseId);
        if (course == null)
            return ServiceResult<Course>.FailureResult("Course not found.");

        return ServiceResult<Course>.SuccessResult(course);
    }

    public async Task<ServiceResult<IEnumerable<Course>>> GetAllAsync()
    {
        var courses = await _unitOfWork.Courses.FindAsync(c => c.IsActive);
        return ServiceResult<IEnumerable<Course>>.SuccessResult(courses);
    }

    public async Task<ServiceResult<Course>> CreateAsync(Course course)
    {
        var exists = await _unitOfWork.Courses.ExistsAsync(c => c.CourseCode == course.CourseCode);
        if (exists)
            return ServiceResult<Course>.FailureResult("Course code already exists.");

        course.IsActive = true;
        course.CreatedAt = DateTime.UtcNow;

        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Course created: {Code} - {Name}", course.CourseCode, course.CourseName);
        return ServiceResult<Course>.SuccessResult(course, "Course created successfully.");
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int courseId)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(courseId);
        if (course == null)
            return ServiceResult<bool>.FailureResult("Course not found.");

        course.IsActive = false;
        _unitOfWork.Courses.Update(course);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Course deactivated: {CourseId}", courseId);
        return ServiceResult<bool>.SuccessResult(true, "Course deleted successfully.");
    }
}
