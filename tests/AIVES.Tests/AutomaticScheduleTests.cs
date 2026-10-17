using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Services;
using AIVES.DataAccessLayer.Models;
using Xunit;
namespace AIVES.Tests;
public class AutomaticScheduleTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(20)]
    public void Arrange_Interval_GeneratesAdjacentSlotsAndEnd(int minutes)
    {
        var start = new DateTime(2026, 10, 7, 8, 0, 0);
        var exam = new Exam { StartsAt = start, EndsAt = start, DurationMinutesPerStudent = minutes };
        var participants = Enumerable.Range(1, 4).Select(i => new ExamParticipant { StudentId = i, SlotOrder = i }).Reverse().ToArray();
        Assert.Null(ExamRules.ArrangeSchedule(exam, participants));
        Assert.Equal(start.AddMinutes(4 * minutes), exam.EndsAt);
        foreach (var p in participants)
        {
            Assert.Equal(start.AddMinutes((p.SlotOrder - 1) * minutes), p.ScheduledStartAt);
            Assert.Equal(start.AddMinutes(p.SlotOrder * minutes), p.ScheduledEndAt);
        }
        Assert.Null(ExamRules.ValidateSchedule(exam, participants));
    }
    [Fact]
    public void Arrange_ChangeStartAndInterval_RecalculatesStoredSlots()
    {
        var exam = new Exam { StartsAt = new DateTime(2026, 10, 7, 9, 0, 0), DurationMinutesPerStudent = 20 };
        var participants = new[] { new ExamParticipant { StudentId = 1, SlotOrder = 1 }, new ExamParticipant { StudentId = 2, SlotOrder = 3 } };
        Assert.Null(ExamRules.ArrangeSchedule(exam, participants));
        exam.StartsAt = exam.StartsAt.AddHours(1);
        exam.DurationMinutesPerStudent = 15;
        Assert.Null(ExamRules.ArrangeSchedule(exam, participants));
        Assert.Equal(exam.StartsAt.AddMinutes(15), participants[1].ScheduledStartAt);
        Assert.Equal(exam.StartsAt.AddMinutes(30), exam.EndsAt);
    }
    [Fact]
    public void Arrange_NoParticipants_ReservesOneIntervalWithoutZeroDuration()
    {
        var exam = new Exam { StartsAt = new DateTime(2026, 10, 7, 8, 0, 0), DurationMinutesPerStudent = 20 };
        Assert.Null(ExamRules.ArrangeSchedule(exam, Array.Empty<ExamParticipant>()));
        Assert.Equal(exam.StartsAt.AddMinutes(20), exam.EndsAt);
    }
    [Fact]
    public void Arrange_InvalidInterval_RejectsWithoutChangingEnd()
    {
        var end = new DateTime(2026, 10, 7, 9, 0, 0);
        var exam = new Exam { StartsAt = end.AddHours(-1), EndsAt = end, DurationMinutesPerStudent = 0 };
        Assert.NotNull(ExamRules.ArrangeSchedule(exam, Array.Empty<ExamParticipant>()));
        Assert.Equal(end, exam.EndsAt);
    }
    [Fact]
    public void AutomaticEnd_IgnoresCallerEnd_UsesStudentCount()
    {
        var start = new DateTime(2026, 10, 7, 8, 0, 0);
        var command = new ExamCommand(1, "Exam", null, start, start.AddMinutes(-1), 20, 3, 2, "RANDOM", 1);
        Assert.Equal(start.AddMinutes(100), ExamRules.WithAutomaticEnd(command, 5).EndsAt);
    }
}