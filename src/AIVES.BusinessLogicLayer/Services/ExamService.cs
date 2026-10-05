using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
namespace AIVES.BusinessLogicLayer.Services;
public class ExamService(IUnitOfWork uow, IUserService users, ICourseService courses) : IExamService
{
    public async Task<ServiceResult<Exam>> GetOwnedAsync(long examId, long lecturerId, bool draftOnly = false)
    {
        var identity = await users.GetIdentityAsync(lecturerId);
        var exam = await uow.Exams.GetByIdAsync(examId);
        if (identity == null || !identity.Roles.Contains("LECTURER") || exam == null ||
            exam.CreatedByUserId != lecturerId ||
            !await uow.CourseLecturers.ExistsAsync(x => x.CourseId == exam.CourseId && x.LecturerId == lecturerId))
            return ServiceResult<Exam>.FailureResult("Không có quyền quản lý kỳ thi này.");
        if (draftOnly && exam.Status != "DRAFT") return ServiceResult<Exam>.FailureResult("Chỉ được thay đổi kỳ thi DRAFT.");
        return ServiceResult<Exam>.SuccessResult(exam);
    }
    public async Task<IReadOnlyList<ExamSummary>> GetAllAsync(long lecturerId)
    {
        var ownedCourses = await courses.GetForLecturerAsync(lecturerId);
        var ids = ownedCourses.Select(x => x.CourseId).ToArray();
        var exams = await uow.Exams.FindAsync(x => x.CreatedByUserId == lecturerId && ids.Contains(x.CourseId));
        return exams.OrderByDescending(x => x.CreatedAt).Select(x => new ExamSummary(x, ownedCourses.Single(c => c.CourseId == x.CourseId).CourseName)).ToArray();
    }
    public async Task<ServiceResult<ExamDetails>> GetDetailsAsync(long examId, long lecturerId)
    {
        var access = await GetOwnedAsync(examId, lecturerId);
        if (!access.Success) return ServiceResult<ExamDetails>.FailureResult(access.Message);
        var e = access.Data!;
        var course = (await uow.Courses.GetByIdAsync(e.CourseId))!;
        var enrollments = await uow.CourseStudents.FindAsync(x => x.CourseId == e.CourseId && x.IsActive);
        var studentRole = (await uow.Roles.FindAsync(x => x.Code == "STUDENT")).SingleOrDefault();
        var roleLinks = studentRole == null ? Array.Empty<UserRole>() : (await uow.UserRoles.FindAsync(x => x.RoleId == studentRole.RoleId)).ToArray();
        var eligibleIds = enrollments.Select(x => x.StudentId).Intersect(roleLinks.Select(x => x.UserId)).ToArray();
        var eligible = (await uow.Users.FindAsync(x => eligibleIds.Contains(x.UserId) && x.Status == "ACTIVE")).OrderBy(x => x.Email).ToArray();
        var participants = (await uow.Participants.FindAsync(x => x.ExamId == examId)).OrderBy(x => x.ScheduledStartAt).ThenBy(x => x.SlotOrder).ToArray();
        var participantIds = participants.Select(x => x.ParticipantId).ToArray();
        var allStudentIds = participants.Select(x => x.StudentId).ToArray();
        var participantUsers = (await uow.Users.FindAsync(x => allStudentIds.Contains(x.UserId))).ToDictionary(x => x.UserId);
        var questionList = (await uow.Questions.FindAsync(x => x.CourseId == e.CourseId)).ToArray();
        var pool = (await uow.QuestionPool.FindAsync(x => x.ExamId == examId && x.IsEnabled)).Select(x => x.QuestionId).ToArray();
        var assignments = (await uow.Assignments.FindAsync(x => participantIds.Contains(x.ParticipantId))).ToArray();
        var preview = new List<AssignmentView>();
        for (var index = 0; index < participants.Length; index++)
        {
            var recentParticipants = participants.Take(index).TakeLast(e.AvoidRecentDuplicateCount).Select(x => x.ParticipantId).ToHashSet();
            var recentQuestions = assignments.Where(x => recentParticipants.Contains(x.ParticipantId)).Select(x => x.QuestionId).ToHashSet();
            foreach (var a in assignments.Where(x => x.ParticipantId == participants[index].ParticipantId).OrderBy(x => x.SequenceNo))
            {
                var q = questionList.SingleOrDefault(x => x.QuestionId == a.QuestionId);
                if (q != null) preview.Add(new(a.ParticipantId, a.SequenceNo, q, recentQuestions.Contains(q.QuestionId)));
            }
        }
        return ServiceResult<ExamDetails>.SuccessResult(new(e, course, eligible,
            participants.Select(p => new ParticipantView(p, participantUsers[p.StudentId].FullName, participantUsers[p.StudentId].Email)).ToArray(),
            questionList, pool, preview));
    }
    public async Task<ServiceResult<Exam>> CreateAsync(ExamCommand c, long lecturerId)
    {
        c = ExamRules.WithAutomaticEnd(c, 0);
        var error = ExamRules.Validate(c);
        var identity = await users.GetIdentityAsync(lecturerId);
        if (error != null) return ServiceResult<Exam>.FailureResult(error);
        if (identity == null || !identity.Roles.Contains("LECTURER") ||
            !(await courses.GetForLecturerAsync(lecturerId)).Any(x => x.CourseId == c.CourseId))
            return ServiceResult<Exam>.FailureResult("Bạn không phụ trách môn học này.");
        var exam = new Exam { CreatedByUserId = lecturerId };
        Apply(exam, c);
        await uow.Exams.AddAsync(exam);
        await uow.SaveChangesAsync();
        return ServiceResult<Exam>.SuccessResult(exam);
    }
    public Task<ServiceResult<Exam>> UpdateAsync(long examId, ExamCommand c, long lecturerId) =>
        uow.InTransactionAsync(examId, async () =>
        {
            var access = await GetOwnedAsync(examId, lecturerId, true);
            if (!access.Success) return access;
            var participants = (await uow.Participants.FindAsync(x => x.ExamId == examId)).ToArray();
            c = ExamRules.WithAutomaticEnd(c, participants.Length);
            var error = ExamRules.Validate(c);
            if (error != null) return ServiceResult<Exam>.FailureResult(error);
            var e = access.Data!;
            if (c.CourseId != e.CourseId) return ServiceResult<Exam>.FailureResult("Không đổi môn học sau khi tạo kỳ thi.");
            // Configuration edits invalidate previews; regeneration is an explicit next step.
            await ClearAssignmentsAsync(examId);
            Apply(e, c);
            var scheduleError = ExamRules.ArrangeSchedule(e, participants);
            if (scheduleError != null) return ServiceResult<Exam>.FailureResult(scheduleError);
            await uow.SaveChangesAsync();
            return ServiceResult<Exam>.SuccessResult(e, "Đã lưu cấu hình và tự xếp lại lịch. Hãy tạo lại bộ câu hỏi trước khi công bố.");
        }, x => x.Success);

    public Task<ServiceResult<bool>> DeleteAsync(long examId, long lecturerId) =>
        uow.InTransactionAsync(examId, async () =>
        {
            var access = await GetOwnedAsync(examId, lecturerId);
            if (!access.Success) return Fail(access.Message);

            // Remove dependent rows before the exam; the database restricts FK deletion.
            await ClearAssignmentsAsync(examId);
            uow.QuestionPool.RemoveRange(await uow.QuestionPool.FindAsync(x => x.ExamId == examId));
            uow.Participants.RemoveRange(await uow.Participants.FindAsync(x => x.ExamId == examId));
            await uow.SaveChangesAsync();
            uow.Exams.Remove(access.Data!);
            await uow.SaveChangesAsync();
            return Ok("Đã xóa kỳ thi.");
        }, x => x.Success);

    public Task<ServiceResult<bool>> SetParticipantsAsync(long examId, IReadOnlyList<long> studentIds, long lecturerId) =>
        Mutate(examId, lecturerId, async e =>
        {
            if (studentIds.Count != studentIds.Distinct().Count()) return Fail("Danh sách sinh viên bị trùng.");
            var detail = (await GetDetailsAsync(examId, lecturerId)).Data!;
            var valid = detail.EligibleStudents.Select(x => x.UserId).ToHashSet();
            if (studentIds.Any(x => !valid.Contains(x))) return Fail("Sinh viên phải đang hoạt động, có role STUDENT và thuộc môn học.");
            await ClearAssignmentsAsync(examId);
            var existing = (await uow.Participants.FindAsync(x => x.ExamId == examId)).ToArray();
            uow.Participants.RemoveRange(existing.Where(x => !studentIds.Contains(x.StudentId)));
            var order = existing.Length == 0 ? 0 : existing.Max(x => x.SlotOrder);
            foreach (var id in studentIds.Where(x => !existing.Any(p => p.StudentId == x)))
                await uow.Participants.AddAsync(new ExamParticipant { ExamId = examId, StudentId = id, SlotOrder = ++order, ScheduledStartAt = e.StartsAt, ScheduledEndAt = e.StartsAt });
            await uow.SaveChangesAsync();
            var participants = (await uow.Participants.FindAsync(x => x.ExamId == examId)).ToArray();
            var error = ExamRules.ArrangeSchedule(e, participants);
            if (error != null) return Fail(error);
            await uow.SaveChangesAsync();
            return Ok($"Đã lưu {participants.Length} thí sinh và tự xếp lịch cách nhau {e.DurationMinutesPerStudent} phút. Kết thúc dự kiến {e.EndsAt:dd/MM HH:mm}.");
        });

    public Task<ServiceResult<bool>> SetQuestionPoolAsync(long examId, IReadOnlyList<long> questionIds, long lecturerId) =>
        Mutate(examId, lecturerId, async e =>
        {
            if (questionIds.Count != questionIds.Distinct().Count()) return Fail("Câu hỏi trong pool bị trùng.");
            var valid = (await uow.Questions.FindAsync(x => x.CourseId == e.CourseId && x.IsActive && x.IsApproved)).Select(x => x.QuestionId).ToHashSet();
            if (questionIds.Any(x => !valid.Contains(x))) return Fail("Pool chỉ nhận câu hỏi approved/active đúng môn học.");
            await ClearAssignmentsAsync(examId);
            var existing = (await uow.QuestionPool.FindAsync(x => x.ExamId == examId)).ToArray();
            foreach (var item in existing) item.IsEnabled = questionIds.Contains(item.QuestionId);
            foreach (var id in questionIds.Where(x => !existing.Any(p => p.QuestionId == x)))
                await uow.QuestionPool.AddAsync(new ExamQuestionPool { ExamId = examId, QuestionId = id });
            await uow.SaveChangesAsync();
            return Ok(questionIds.Count < e.MainQuestionCount
                ? "Đã lưu lựa chọn. " + ExamRules.PoolShortage(questionIds.Count, e.MainQuestionCount)
                : $"Đã lưu {questionIds.Count} câu trong ngân hàng kỳ thi. Mỗi sinh viên sẽ được chọn {e.MainQuestionCount} câu chính.");
        });

    public Task<ServiceResult<bool>> PublishAsync(long examId, long lecturerId) =>
        Mutate(examId, lecturerId, async e =>
        {
            var detail = (await GetDetailsAsync(examId, lecturerId)).Data!;
            var participants = detail.Participants.Select(x => x.Participant).ToArray();
            var error = ExamRules.ValidateSchedule(e, participants);
            if (error != null) return Fail(error);
            var eligible = detail.EligibleStudents.Select(x => x.UserId).ToHashSet();
            if (participants.Any(x => !eligible.Contains(x.StudentId))) return Fail("Có thí sinh không còn đủ điều kiện.");
            var validQuestions = detail.Questions.Where(x => x.IsActive && x.IsApproved).Select(x => x.QuestionId).ToHashSet();
            if (detail.SelectedQuestionIds.Count < e.MainQuestionCount)
                return Fail(ExamRules.PoolShortage(detail.SelectedQuestionIds.Count, e.MainQuestionCount));
            if (detail.SelectedQuestionIds.Any(x => !validQuestions.Contains(x)))
                return Fail("Có câu hỏi không còn hoạt động hoặc chưa được duyệt. Chọn lại ngân hàng câu hỏi trước khi công bố.");
            foreach (var p in participants)
            {
                var assigned = detail.Assignments.Where(x => x.ParticipantId == p.ParticipantId).ToArray();
                if (assigned.Length != e.MainQuestionCount || assigned.Select(x => x.Question.QuestionId).Distinct().Count() != e.MainQuestionCount ||
                    !assigned.Select(x => x.SequenceNo).Order().SequenceEqual(Enumerable.Range(1, e.MainQuestionCount)) ||
                    assigned.Any(x => !detail.SelectedQuestionIds.Contains(x.Question.QuestionId)))
                    return Fail("Bộ câu hỏi chưa đầy đủ/hợp lệ. Hãy Generate Assignments.");
            }
            e.Status = "PUBLISHED";
            e.UpdatedAt = DateTime.UtcNow;
            foreach (var p in participants) { p.Status = "READY"; p.UpdatedAt = DateTime.UtcNow; }
            await uow.SaveChangesAsync();
            return Ok("Đã Publish. Sinh viên có thể xem lịch của mình.");
        });
    private Task<ServiceResult<bool>> Mutate(long id, long owner, Func<Exam, Task<ServiceResult<bool>>> action) =>
        uow.InTransactionAsync(id, async () =>
        {
            var access = await GetOwnedAsync(id, owner, true);
            return access.Success ? await action(access.Data!) : Fail(access.Message);
        }, x => x.Success);
    public async Task ClearAssignmentsAsync(long examId)
    {
        var ids = (await uow.Participants.FindAsync(x => x.ExamId == examId)).Select(x => x.ParticipantId).ToArray();
        uow.Assignments.RemoveRange(await uow.Assignments.FindAsync(x => ids.Contains(x.ParticipantId)));
        await uow.SaveChangesAsync();
    }
    private static void Apply(Exam e, ExamCommand c)
    {
        e.CourseId = c.CourseId; e.Title = c.Title.Trim(); e.Description = c.Description;
        e.StartsAt = c.StartsAt; e.EndsAt = c.EndsAt; e.DurationMinutesPerStudent = c.DurationMinutesPerStudent;
        e.MainQuestionCount = c.MainQuestionCount; e.MaxFollowUpPerQuestion = c.MaxFollowUpPerQuestion;
        e.SelectionStrategy = c.SelectionStrategy; e.AvoidRecentDuplicateCount = c.AvoidRecentDuplicateCount; e.UpdatedAt = DateTime.UtcNow;
    }
    private static ServiceResult<bool> Fail(string m) => ServiceResult<bool>.FailureResult(m);
    private static ServiceResult<bool> Ok(string m) => ServiceResult<bool>.SuccessResult(true, m);
}
