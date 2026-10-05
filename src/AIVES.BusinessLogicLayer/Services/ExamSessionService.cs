using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
namespace AIVES.BusinessLogicLayer.Services;
/// <summary>Function 2 scheduling; live oral-answer scoring is outside this schema.</summary>
public class ExamSessionService(IUnitOfWork uow, IExamService exams) : IExamSessionService
{
    public Task<ServiceResult<bool>> GenerateScheduleAsync(long examId, long lecturerId, DateTime? startsAt = null, int? minutesPerStudent = null) =>
        uow.InTransactionAsync(examId, async () =>
        {
            var access = await exams.GetOwnedAsync(examId, lecturerId, true);
            if (!access.Success) return Fail(access.Message);
            var e = access.Data!;
            var participants = (await uow.Participants.FindAsync(x => x.ExamId == examId)).OrderBy(x => x.SlotOrder).ToArray();
            if (participants.Length == 0) return Fail("Cần chọn thí sinh trước.");
            if (startsAt.HasValue) e.StartsAt = startsAt.Value;
            if (minutesPerStudent.HasValue) e.DurationMinutesPerStudent = minutesPerStudent.Value;
            var arrangeError = ExamRules.ArrangeSchedule(e, participants);
            if (arrangeError != null) return Fail(arrangeError);
            var error = ExamRules.ValidateSchedule(e, participants);
            if (error != null) return Fail(error);
            await ClearAssignmentsAsync(participants);
            await uow.SaveChangesAsync();
            return Ok($"Đã tự xếp {participants.Length} lượt thi, mỗi lượt {e.DurationMinutesPerStudent} phút. Kết thúc {e.EndsAt:dd/MM HH:mm}. Hãy tạo lại bộ câu hỏi theo lịch mới.");
        }, x => x.Success);
    public Task<ServiceResult<bool>> UpdateSlotsAsync(long examId, IReadOnlyList<SlotCommand> slots, long lecturerId) =>
        uow.InTransactionAsync(examId, async () =>
        {
            var access = await exams.GetOwnedAsync(examId, lecturerId, true);
            if (!access.Success) return Fail(access.Message);
            var participants = (await uow.Participants.FindAsync(x => x.ExamId == examId)).ToArray();
            if (slots.Count != participants.Length || slots.Select(x => x.ParticipantId).Distinct().Count() != participants.Length ||
                slots.Any(x => !participants.Any(p => p.ParticipantId == x.ParticipantId)))
                return Fail("Phải gửi đầy đủ, đúng danh sách slot của kỳ thi.");
            foreach (var p in participants)
            {
                var slot = slots.Single(x => x.ParticipantId == p.ParticipantId);
                p.ScheduledStartAt = slot.Start;
                p.ScheduledEndAt = slot.End;
                p.UpdatedAt = DateTime.UtcNow;
            }
            var error = ExamRules.ValidateSchedule(access.Data!, participants);
            if (error != null) return Fail(error);
            await ClearAssignmentsAsync(participants);
            await uow.SaveChangesAsync();
            return Ok("Đã sửa lịch. Hãy tạo lại bộ câu hỏi.");
        }, x => x.Success);
    private async Task ClearAssignmentsAsync(ExamParticipant[] participants)
    {
        var ids = participants.Select(x => x.ParticipantId).ToArray();
        uow.Assignments.RemoveRange(await uow.Assignments.FindAsync(x => ids.Contains(x.ParticipantId)));
    }
    public async Task<IReadOnlyList<StudentSchedule>> GetMyScheduleAsync(long studentId)
    {
        var participants = (await uow.Participants.FindAsync(x => x.StudentId == studentId)).ToArray();
        var examIds = participants.Select(x => x.ExamId).ToArray();
        var examList = (await uow.Exams.FindAsync(x => examIds.Contains(x.ExamId) && x.Status != "DRAFT")).ToDictionary(x => x.ExamId);
        var courseIds = examList.Values.Select(x => x.CourseId).ToArray();
        var courses = (await uow.Courses.FindAsync(x => courseIds.Contains(x.CourseId))).ToDictionary(x => x.CourseId);
        return participants.Where(p => examList.ContainsKey(p.ExamId)).OrderBy(x => x.ScheduledStartAt).Select(p =>
        {
            var e = examList[p.ExamId];
            return new StudentSchedule(p.ParticipantId, e.Title, courses[e.CourseId].CourseName, p.ScheduledStartAt,
                p.ScheduledEndAt, e.DurationMinutesPerStudent, e.Status);
        }).ToArray();
    }
    private static ServiceResult<bool> Fail(string m) => ServiceResult<bool>.FailureResult(m);
    private static ServiceResult<bool> Ok(string m) => ServiceResult<bool>.SuccessResult(true, m);
}
