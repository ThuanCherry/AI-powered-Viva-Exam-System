using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Services;
using AIVES.DataAccessLayer.Models;
using Xunit;
namespace AIVES.Tests;
public class ExamRulesTests
{
    private readonly DateTime _start = new(2026, 10, 6, 8, 0, 0);
    private Exam Exam => new() { StartsAt = _start, EndsAt = _start.AddHours(2), DurationMinutesPerStudent = 15 };
    private ExamParticipant P(long student, int order, int start, int end) =>
        new() { StudentId = student, SlotOrder = order, ScheduledStartAt = _start.AddMinutes(start), ScheduledEndAt = _start.AddMinutes(end) };
    [Fact] public void Schedule_AdjacentSlots_Accepts() => Assert.Null(ExamRules.ValidateSchedule(Exam, new[] { P(1, 1, 0, 15), P(2, 2, 15, 30) }));
    [Fact] public void Schedule_Overlap_Rejects() => Assert.NotNull(ExamRules.ValidateSchedule(Exam, new[] { P(1, 1, 0, 15), P(2, 2, 10, 25) }));
    [Fact] public void Schedule_BeyondEnd_Rejects() => Assert.NotNull(ExamRules.ValidateSchedule(Exam, new[] { P(1, 1, 115, 130) }));
    [Fact] public void Schedule_DuplicateStudent_Rejects() => Assert.NotNull(ExamRules.ValidateSchedule(Exam, new[] { P(1, 1, 0, 15), P(1, 2, 15, 30) }));
    [Fact] public void Schedule_ZeroDuration_Rejects() => Assert.NotNull(ExamRules.ValidateSchedule(Exam, new[] { P(1, 1, 0, 0) }));
    [Fact] public void Schedule_NoParticipants_Rejects() => Assert.NotNull(ExamRules.ValidateSchedule(Exam, Array.Empty<ExamParticipant>()));
    [Theory]
    [InlineData(0, 3, 2, "RANDOM", 1)]
    [InlineData(15, 0, 2, "RANDOM", 1)]
    [InlineData(15, 3, -1, "RANDOM", 1)]
    [InlineData(15, 3, 2, "UNKNOWN", 1)]
    [InlineData(15, 3, 2, "RANDOM", -1)]
    public void Configuration_InvalidInput_Rejects(int duration, int main, int follow, string strategy, int recent)
    {
        Assert.NotNull(ExamRules.Validate(new ExamCommand(1, "Exam", null, _start, _start.AddHours(2), duration, main, follow, strategy, recent)));
    }
    [Fact]
    public void Configuration_ReversedTimes_Rejects() =>
        Assert.NotNull(ExamRules.Validate(new ExamCommand(1, "Exam", null, _start.AddHours(2), _start, 15, 3, 2, "RANDOM", 1)));
}