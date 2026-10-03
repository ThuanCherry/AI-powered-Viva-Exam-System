using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.Extensions.Logging;

namespace AIVES.BusinessLogicLayer.Services;

public class ExamService : IExamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExamService> _logger;

    public ExamService(IUnitOfWork unitOfWork, ILogger<ExamService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ServiceResult<Exam>> GetByIdAsync(int examId)
    {
        var exam = await _unitOfWork.Exams.GetByIdAsync(examId);
        if (exam == null)
            return ServiceResult<Exam>.FailureResult("Exam not found.");

        return ServiceResult<Exam>.SuccessResult(exam);
    }

    public async Task<ServiceResult<IEnumerable<Exam>>> GetAllAsync()
    {
        var exams = await _unitOfWork.Exams.FindAsync(e => e.IsActive);
        return ServiceResult<IEnumerable<Exam>>.SuccessResult(exams);
    }

    public async Task<ServiceResult<IEnumerable<Exam>>> GetBySubjectAsync(string subject)
    {
        var exams = await _unitOfWork.Exams.FindAsync(e => e.Subject == subject && e.IsActive);
        return ServiceResult<IEnumerable<Exam>>.SuccessResult(exams);
    }

    public async Task<ServiceResult<Exam>> CreateAsync(Exam exam, IEnumerable<Question> questions, int createdByUserId)
    {
        var creator = await _unitOfWork.Users.GetByIdAsync(createdByUserId);
        if (creator == null)
            return ServiceResult<Exam>.FailureResult("Creator user not found.");

        exam.CreatedByUserId = createdByUserId;
        exam.CreatedAt = DateTime.UtcNow;
        exam.IsActive = true;
        var questionList = questions.ToList();
        exam.TotalQuestions = questionList.Count;

        await _unitOfWork.Exams.AddAsync(exam);
        await _unitOfWork.SaveChangesAsync();

        int index = 1;
        foreach (var q in questionList)
        {
            q.ExamId = exam.ExamId;
            q.OrderIndex = index++;
            q.CreatedAt = DateTime.UtcNow;
        }

        if (questionList.Count > 0)
        {
            await _unitOfWork.Questions.AddRangeAsync(questionList);
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Exam created: {Title} with {Count} questions", exam.Title, questionList.Count);
        return ServiceResult<Exam>.SuccessResult(exam, "Exam created successfully.");
    }

    public async Task<ServiceResult<Exam>> UpdateAsync(int examId, Exam updatedExam)
    {
        var exam = await _unitOfWork.Exams.GetByIdAsync(examId);
        if (exam == null)
            return ServiceResult<Exam>.FailureResult("Exam not found.");

        exam.Title = updatedExam.Title;
        exam.Description = updatedExam.Description;
        exam.Subject = updatedExam.Subject;
        exam.DurationMinutes = updatedExam.DurationMinutes;
        exam.Difficulty = updatedExam.Difficulty;

        _unitOfWork.Exams.Update(exam);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Exam updated: {ExamId}", examId);
        return ServiceResult<Exam>.SuccessResult(exam, "Exam updated successfully.");
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int examId)
    {
        var exam = await _unitOfWork.Exams.GetByIdAsync(examId);
        if (exam == null)
            return ServiceResult<bool>.FailureResult("Exam not found.");

        exam.IsActive = false;
        _unitOfWork.Exams.Update(exam);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Exam soft-deleted: {ExamId}", examId);
        return ServiceResult<bool>.SuccessResult(true, "Exam deleted successfully.");
    }
}
