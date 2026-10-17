using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
namespace AIVES.BusinessLogicLayer.Services;
public class QuestionAssignmentService(IUnitOfWork uow, IExamService exams, IEnumerable<IQuestionSelectionStrategy> strategies) : IQuestionAssignmentService
{
    public Task<ServiceResult<bool>> GenerateAsync(long examId, long lecturerId) =>
        uow.InTransactionAsync(examId, async () =>
        {
            var access = await exams.GetOwnedAsync(examId, lecturerId, true);
            if (!access.Success) return Fail(access.Message);
            var e = access.Data!;
            var participants = (await uow.Participants.FindAsync(x => x.ExamId == examId)).OrderBy(x => x.ScheduledStartAt).ThenBy(x => x.SlotOrder).ToArray();
            var error = ExamRules.ValidateSchedule(e, participants);
            if (error != null) return Fail(error);
            var poolIds = (await uow.QuestionPool.FindAsync(x => x.ExamId == examId && x.IsEnabled)).Select(x => x.QuestionId).ToArray();
            var candidates = (await uow.Questions.FindAsync(x => poolIds.Contains(x.QuestionId) && x.CourseId == e.CourseId && x.IsActive && x.IsApproved)).ToArray();
            if (candidates.Length < e.MainQuestionCount) return Fail(ExamRules.PoolShortage(candidates.Length, e.MainQuestionCount));
            var strategy = strategies.SingleOrDefault(x => x.Name == e.SelectionStrategy);
            if (strategy == null) return Fail("Chiến lược không hợp lệ.");
            var history = new Queue<IReadOnlyList<Question>>();
            var assignments = new List<ExamQuestionAssignment>();
            var repeats = 0;
            foreach (var p in participants)
            {
                var recent = history.SelectMany(x => x).Select(x => x.QuestionId).ToHashSet();
                var selected = strategy.Select(candidates, e.MainQuestionCount, recent);
                if (selected.Count != e.MainQuestionCount || selected.Select(x => x.QuestionId).Distinct().Count() != selected.Count)
                    return Fail("Không thể tạo đủ bộ câu hỏi.");
                repeats += selected.Count(x => recent.Contains(x.QuestionId));
                for (int i = 0; i < selected.Count; i++)
                    assignments.Add(new ExamQuestionAssignment { ParticipantId = p.ParticipantId, QuestionId = selected[i].QuestionId, SequenceNo = i + 1 });
                history.Enqueue(selected);
                while (history.Count > e.AvoidRecentDuplicateCount) history.Dequeue();
            }
            var ids = participants.Select(x => x.ParticipantId).ToArray();
            uow.Assignments.RemoveRange(await uow.Assignments.FindAsync(x => ids.Contains(x.ParticipantId)));
            await uow.SaveChangesAsync();
            await uow.Assignments.AddRangeAsync(assignments);
            await uow.SaveChangesAsync();
            var fallback = e.SelectionStrategy == "ADAPTIVE" && candidates.All(x => string.IsNullOrWhiteSpace(x.Difficulty) && string.IsNullOrWhiteSpace(x.BloomLevel));
            return ServiceResult<bool>.SuccessResult(true, $"Đã tạo bộ câu hỏi. Câu lặp gần nhau do pool nhỏ: {repeats}." +
                (fallback ? " Không có metadata: fallback RANDOM." : ""));
        }, x => x.Success);
    private static ServiceResult<bool> Fail(string m) => ServiceResult<bool>.FailureResult(m);
}
