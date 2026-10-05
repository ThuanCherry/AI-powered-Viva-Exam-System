using AIVES.BusinessLogicLayer.Common;
namespace AIVES.BusinessLogicLayer.Interfaces;
public interface IExamSessionService
{
    Task<ServiceResult<bool>> GenerateScheduleAsync(long examId, long lecturerId, DateTime? startsAt = null, int? minutesPerStudent = null);
    Task<ServiceResult<bool>> UpdateSlotsAsync(long examId, IReadOnlyList<SlotCommand> slots, long lecturerId);
    Task<IReadOnlyList<StudentSchedule>> GetMyScheduleAsync(long studentId);
}
