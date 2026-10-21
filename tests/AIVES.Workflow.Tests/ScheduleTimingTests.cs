using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Models;
using AIVES.Lecturer.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIVES.Workflow.Tests;

// Filter this class only. No interview, STT, TTS or provider endpoint is called.
public class ScheduleTimingTests
{
    [Theory]
    [InlineData("2026-10-19T03:18:00+07:00", 15, 2, "2026-10-19T03:48:00+07:00")]
    [InlineData("2028-02-28T23:50:00+07:00", 20, 2, "2028-02-29T00:30:00+07:00")]
    [InlineData("2027-12-31T23:50:00+07:00", 15, 2, "2028-01-01T00:20:00+07:00")]
    [InlineData("2026-10-31T23:45:00+07:00", 15, 3, "2026-11-01T00:30:00+07:00")]
    [InlineData("2026-10-19T08:00:00Z", 180, 500, "2026-12-20T20:00:00Z")]
    public void EndIsDerivedAcrossMidnightMonthYearAndLongSchedule(string start, int minutes, int count, string expected)
    {
        var input = new ExamInput(1, "Exam", null, DateTimeOffset.Parse(start), null, minutes, 1, 0, 0, 120, "Random", Enumerable.Range(1, count).ToArray(), 1);
        Assert.Equal(DateTimeOffset.Parse(expected), WorkflowRules.ScheduleEnd(input));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(-1, 1)] [InlineData(181, 1)] [InlineData(15, 0)] [InlineData(15, 501)]
    public void InvalidDurationOrRosterIsRejected(int minutes, int count)
    {
        var input = new ExamInput(1, "Exam", null, DateTimeOffset.UtcNow, null, minutes, 1, 0, 0, 120, "Random", Enumerable.Range(1, count).ToArray(), 1);
        Assert.Equal(400, Assert.Throws<WorkflowException>(() => WorkflowRules.ScheduleEnd(input)).Status);
    }

    [Fact] public void DuplicateStudentsAndDateOverflowAreRejected()
    {
        var input = new ExamInput(1, "Exam", null, DateTimeOffset.MaxValue.AddMinutes(-1), null, 15, 1, 0, 0, 120, "Random", [2], 1);
        Assert.Equal(400, Assert.Throws<WorkflowException>(() => WorkflowRules.ScheduleEnd(input)).Status);
        Assert.Equal(400, Assert.Throws<WorkflowException>(() => WorkflowRules.ScheduleEnd(input with { StartTime = DateTimeOffset.UtcNow, StudentIds = [2, 2] })).Status);
    }

    [Fact] public async Task SaveIgnoresClientEndAndRecalculatesSlotsWhenRosterAndDurationChange()
    {
        await using var f = await Fixture.Create();
        var start = DateTimeOffset.Parse("2026-10-20T23:50:00+07:00");
        var input = f.Input(students: [2, 3]) with { StartTime = start, EndTime = start.AddMinutes(-1) };
        var exam = await f.Flow.Save(input, 1, null, default);
        Assert.Equal(start.AddMinutes(30).UtcDateTime, exam.EndTime);
        Assert.Equal(start.UtcDateTime, exam.Candidates.Single(c => c.StudentId == 2).ScheduledTime);
        Assert.Equal(start.AddMinutes(15).UtcDateTime, exam.Candidates.Single(c => c.StudentId == 3).ScheduledTime);
        await f.Flow.Save(input with { StudentIds = [3, 2], TimePerStudent = 25, EndTime = start.AddDays(10) }, 1, exam.ExamId, default);
        Assert.Equal(start.AddMinutes(50).UtcDateTime, exam.EndTime);
        Assert.Equal(start.AddMinutes(25).UtcDateTime, exam.Candidates.Single(c => c.StudentId == 2).ScheduledTime);
        await f.Flow.Save(input with { StudentIds = [3], TimePerStudent = 10, EndTime = null }, 1, exam.ExamId, default);
        Assert.Equal(start.AddMinutes(10).UtcDateTime, exam.EndTime);
        Assert.Equal("Cancelled", exam.Candidates.Single(c => c.StudentId == 2).Status);
        Assert.Equal(0, f.Llm.Calls); Assert.Equal(0, f.Speech.Calls);
    }

    [Fact] public async Task DerivedWindowRejectsOverlapButAllowsTouchingBoundaryAcrossMidnight()
    {
        await using var f = await Fixture.Create(); var start = DateTimeOffset.Parse("2026-10-20T23:50:00+07:00");
        await f.Flow.Save(f.Input(students: [2, 3]) with { StartTime = start, EndTime = null }, 1, null, default);
        var conflict = await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(f.Input() with { StartTime = start.AddMinutes(29), EndTime = start.AddMinutes(30) }, 1, null, default));
        Assert.Equal(409, conflict.Status);
        var adjacent = await f.Flow.Save(f.Input() with { StartTime = start.AddMinutes(30), EndTime = null }, 1, null, default);
        Assert.Equal(start.AddMinutes(45).UtcDateTime, adjacent.EndTime);
    }

    [Fact] public async Task StudentOverlapAcrossLecturersUsesIndividualSlotAndAllowsAdjacentSlot()
    {
        await using var f = await Fixture.Create(); var start = DateTimeOffset.Parse("2026-10-20T23:50:00+07:00");
        await f.Flow.Save(f.Input(students: [2, 3]) with { StartTime = start, EndTime = null }, 1, null, default);
        f.Db.Users.Add(new User { UserId = 4, RoleId = 1, Email = "other@test.invalid", FullName = "Other" });
        f.Db.Courses.Add(new Course { CourseId = 2, LecturerId = 4, CourseCode = "OTHER", CourseName = "Other" });
        f.Db.AcademicClasses.Add(new AcademicClass { ClassId = 2, CourseId = 2, LecturerId = 4, ClassCode = "C2", ClassName = "C2" });
        f.Db.ClassStudents.Add(new ClassStudent { ClassId = 2, StudentId = 3 });
        f.Db.Questions.Add(new Question { CourseId = 2, CreatedBy = 4, QuestionText = "Manual", Status = "Approved" }); await f.Db.SaveChangesAsync();
        var other = f.Input(main: 1, students: [3]) with { CourseId = 2, ClassId = 2, StartTime = start.AddMinutes(20), EndTime = null };
        Assert.Equal(409, (await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(other, 4, null, default))).Status);
        var next = await f.Flow.Save(other with { StartTime = start.AddMinutes(30) }, 4, null, default);
        Assert.Equal(start.AddMinutes(45).UtcDateTime, next.EndTime);
    }

    [Theory]
    [InlineData("InProgress")] [InlineData("Completed")] [InlineData("Draft")]
    public async Task StartedOrPublishedScheduleCannotBeChanged(string status)
    {
        await using var f = await Fixture.Create(); var input = f.Input() with { StartTime = f.Clock.Value.AddHours(1), EndTime = null };
        var exam = await f.Flow.Save(input, 1, null, default); exam.Status = status; await f.Db.SaveChangesAsync();
        if (status == "Draft") f.Clock.Value = input.StartTime;
        var originalEnd = exam.EndTime; var originalSlot = exam.Candidates.Single().ScheduledTime;
        Assert.Equal(409, (await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(input with { TimePerStudent = 30 }, 1, exam.ExamId, default))).Status);
        Assert.Equal(originalEnd, exam.EndTime); Assert.Equal(originalSlot, exam.Candidates.Single().ScheduledTime);
        Assert.Equal(0, f.Llm.Calls); Assert.Equal(0, f.Speech.Calls);
    }

    [Fact] public async Task HttpCreateWithoutEndTimeAndUpdateReturnComputedEndAndStudentSlotEnd()
    {
        await using var s = await LecturerSandbox.Create();
        using (var scope = s.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
            db.Courses.Add(new Course { CourseId = 1, LecturerId = 1, CourseCode = "TIMING", CourseName = "Timing" });
            db.AcademicClasses.Add(new AcademicClass { ClassId = 1, CourseId = 1, LecturerId = 1, ClassCode = "TIME1", ClassName = "Time" });
            db.ClassStudents.AddRange(new ClassStudent { ClassId = 1, StudentId = 2 }, new ClassStudent { ClassId = 1, StudentId = 3 });
            db.Questions.Add(new Question { CourseId = 1, CreatedBy = 1, QuestionText = "Manual", Status = "Approved" }); await db.SaveChangesAsync();
        }
        s.Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Token());
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddHours(16).AddMinutes(50), TimeSpan.Zero); // 23:50 VN
        var body = new { courseId = 1, classId = 1, examName = "Auto timing", startTime = start, timePerStudent = 20, mainQuestionCount = 1, maxFollowUpCount = 0, maxFollowUpsPerQuestion = 0, secondsPerQuestion = 120, strategy = "Random", studentIds = new[] { 2, 3 } };
        using var create = await s.Http.PostAsJsonAsync("/api/Exams", body); Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("examId").GetInt32();
        var detail = (await s.Http.GetFromJsonAsync<JsonElement>($"/api/Exams/{id}")).GetProperty("data");
        Assert.Equal(start.AddMinutes(40), detail.GetProperty("endTime").GetDateTimeOffset());
        Assert.Equal(start.AddMinutes(20), detail.GetProperty("candidates")[0].GetProperty("scheduledEndTime").GetDateTimeOffset());
        using var update = await s.Http.PutAsJsonAsync($"/api/Exams/{id}", body with { timePerStudent = 30, studentIds = new[] { 3 } }); Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        detail = (await s.Http.GetFromJsonAsync<JsonElement>($"/api/Exams/{id}")).GetProperty("data");
        Assert.Equal(start.AddMinutes(30), detail.GetProperty("endTime").GetDateTimeOffset()); Assert.Single(detail.GetProperty("candidates").EnumerateArray());
        Assert.Equal(0, s.Ai.Calls); Assert.Equal(0, s.Network.Calls);
    }
}
