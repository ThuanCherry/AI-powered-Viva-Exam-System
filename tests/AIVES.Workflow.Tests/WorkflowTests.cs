using System.Text.Json;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using AIVES.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AIVES.Workflow.Tests;

public class TestClock : TimeProvider
{
    public DateTimeOffset Value = new(2026, 10, 19, 2, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Value;
}
public class FakeLlm : IVivaLlm
{
    public int Calls;
    public bool Fail;
    public bool Enough;
    public Task<FollowUpDecision> Decide(string m, string e, string c, string t, string h, CancellationToken ct)
    {
        Calls++;
        if (Fail) throw new WorkflowException(502, "ProviderError test");
        return Task.FromResult(new FollowUpDecision(!Enough, $"Giải thích thêm ý thứ {Calls}?", "Mơ hồ"));
    }
}
public class FakeSpeech : IVivaSpeech
{
    public bool Success;
    public int Calls;
    public Task<string> Transcribe(byte[] wav, CancellationToken ct)
    {
        Calls++;
        return Success ? Task.FromResult("Bản ghi thử") : throw new WorkflowException(503, "NotConfigured test speech");
    }
    public Task<byte[]> Speak(string text, CancellationToken ct) => throw new WorkflowException(503, "NotConfigured test speech");
}

public sealed class Fixture : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public AivesDbContext Db = null!;
    public readonly TestClock Clock = new();
    public readonly FakeLlm Llm = new();
    public readonly FakeSpeech Speech = new();
    public WorkflowService Flow = null!;
    public VivaEngine Engine = null!;
    public static async Task<Fixture> Create()
    {
        var f = new Fixture(); await f.connection.OpenAsync();
        f.Db = new(new DbContextOptionsBuilder<AivesDbContext>().UseSqlite(f.connection).Options);
        await f.Db.Database.EnsureCreatedAsync(); // SQLite memory ONLY, never the configured MySQL.
        f.Db.Roles.AddRange(new Role { RoleId = 1, RoleName = "Lecturer" }, new Role { RoleId = 2, RoleName = "Student" });
        f.Db.Users.AddRange(new User { UserId = 1, RoleId = 1, Email = "l@test.invalid", FullName = "Lecturer" }, new User { UserId = 2, RoleId = 2, Email = "s@test.invalid", FullName = "Student" }, new User { UserId = 3, RoleId = 2, Email = "s2@test.invalid", FullName = "Student2" });
        f.Db.Courses.Add(new Course { CourseId = 1, LecturerId = 1, CourseCode = "SWD", CourseName = "SWD" });
        f.Db.AcademicClasses.Add(new AcademicClass { ClassId = 1, CourseId = 1, LecturerId = 1, ClassCode = "C1", ClassName = "C1" });
        f.Db.ClassStudents.AddRange(new ClassStudent { ClassId = 1, StudentId = 2 }, new ClassStudent { ClassId = 1, StudentId = 3 });
        f.Db.Questions.AddRange(Enumerable.Range(1, 9).Select(i => new Question { QuestionId = i, CourseId = 1, CreatedBy = 1, QuestionText = $"Question {i}", ExpectedAnswer = "secret answer", Status = "Approved", Difficulty = new[] { "Easy", "Medium", "Hard" }[i % 3] }));
        await f.Db.SaveChangesAsync();
        f.Flow = new(f.Db, f.Clock);
        // Existing follow-up limit tests explicitly opt in using mock AI only.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AI:EnableFollowUpOnSubmit"] = "true"
        }).Build();
        f.Engine = new(f.Db, f.Flow, f.Llm, f.Speech, new ProviderStatus(config), config);
        return f;
    }
    public ExamInput Input(int main = 2, int total = 3, int perMain = 2, int[]? students = null) => new(1, "Exam", "Test", Clock.Value, Clock.Value.AddHours(1), 15, main, total, perMain, 30, "Random", students ?? [2], 1);
    public async Task<int> Published(ExamInput? input = null)
    {
        var e = await Flow.Save(input ?? Input(), 1, null, default); await Flow.Publish(e.ExamId, 1, default); return e.ExamId;
    }
    public static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o);
    public async Task<JsonElement> Submit(int id, string text = "Câu trả lời chưa rõ")
    {
        var state = Json(await Engine.Resume(id, 2, default));
        return Json(await Engine.Submit(id, 2, new(state.GetProperty("turn").GetProperty("turnId").GetString()!, text), default));
    }
    public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
}

public class WorkflowTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task SubmitWithoutLlmPersistsTranscriptAdvancesAndIsIdempotent(string? enabled)
    {
        await using var f = await Fixture.Create();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AI:EnableFollowUpOnSubmit"] = enabled
        }).Build();
        f.Engine = new(f.Db, f.Flow, f.Llm, f.Speech, new ProviderStatus(config), config);
        f.Llm.Fail = true; // Any accidental provider call would fail and leave a ProviderError log.
        var id = await f.Published();
        var first = Fixture.Json(await f.Engine.Start(id, 2, default));
        var turn = first.GetProperty("turn").GetProperty("turnId").GetString()!;
        var next = Fixture.Json(await f.Engine.Submit(id, 2, new(turn, "Transcript từ giọng nói"), default));
        Assert.Equal(2, next.GetProperty("turn").GetProperty("mainOrder").GetInt32());
        Assert.False(next.GetProperty("turn").GetProperty("isFollowUp").GetBoolean());
        await f.Engine.Submit(id, 2, new(turn, "Không được ghi đè"), default);
        Assert.Equal("Transcript từ giọng nói", (await f.Db.Answers.SingleAsync()).Transcript);
        var finished = await f.Submit(id, "Câu trả lời thứ hai");
        Assert.Equal("Completed", finished.GetProperty("Status").GetString());
        Assert.Equal(2, await f.Db.Answers.CountAsync());
        Assert.Empty(await f.Db.FollowUpQuestions.ToListAsync());
        Assert.False(await f.Db.ExamLogs.AnyAsync(l => l.Action == "ProviderError"));
        Assert.Equal(0, f.Llm.Calls); Assert.Equal(0, f.Speech.Calls);
    }

    [Fact] public async Task DraftPublishStartResumeAndRetryPersistExactlyOnce()
    {
        await using var f = await Fixture.Create(); var id = await f.Published();
        await f.Flow.Publish(id, 1, default);
        Assert.Equal(2, await f.Db.ExamQuestions.CountAsync());
        var first = Fixture.Json(await f.Engine.Start(id, 2, default));
        var again = Fixture.Json(await f.Engine.Start(id, 2, default));
        Assert.Equal(first.GetProperty("turn").GetProperty("turnId").GetString(), again.GetProperty("turn").GetProperty("turnId").GetString());
        var turn = first.GetProperty("turn").GetProperty("turnId").GetString()!;
        await f.Engine.Submit(id, 2, new(turn, "answer"), default);
        await f.Engine.Submit(id, 2, new(turn, "overwrite"), default);
        Assert.Equal(1, await f.Db.Answers.CountAsync()); Assert.Equal("answer", (await f.Db.Answers.FirstAsync()).Transcript);
        f.Db.ChangeTracker.Clear();
        var resumed = Fixture.Json(await f.Engine.Resume(id, 2, default));
        Assert.True(resumed.GetProperty("turn").GetProperty("isFollowUp").GetBoolean());
        Assert.DoesNotContain("secret answer", resumed.ToString());
    }
    [Fact] public async Task BothFollowupLimitsAreEnforcedAndMainCountCompletes()
    {
        await using var f = await Fixture.Create(); var id = await f.Published(f.Input(2, 3, 2)); await f.Engine.Start(id, 2, default);
        JsonElement state = default;
        for (var i = 0; i < 5; i++) state = await f.Submit(id);
        Assert.Equal("Completed", state.GetProperty("Status").GetString());
        Assert.Equal(3, await f.Db.FollowUpQuestions.CountAsync());
        Assert.All(await f.Db.Answers.Include(a => a.FollowUps).ToListAsync(), a => Assert.InRange(a.FollowUps.Count, 0, 2));
        Assert.Equal(2, await f.Db.ExamQuestions.CountAsync(q => q.IsCompleted));
    }
    [Fact] public async Task EnoughAnswerNeedsNoFollowupAndProviderFailureIsDurable()
    {
        await using var f = await Fixture.Create(); var id = await f.Published(); await f.Engine.Start(id, 2, default);
        f.Llm.Enough = true; await f.Submit(id); Assert.Empty(await f.Db.FollowUpQuestions.ToListAsync());
        f.Llm.Fail = true; var state = await f.Submit(id);
        Assert.Equal("Completed", state.GetProperty("Status").GetString());
        Assert.Equal(2, await f.Db.Answers.CountAsync()); Assert.Contains(await f.Db.ExamLogs.ToListAsync(), l => l.Action == "ProviderError");
    }
    [Fact] public async Task ServerTimeRejectsEarlyLateAndUnauthorizedStart()
    {
        await using var f = await Fixture.Create(); var id = await f.Published();
        f.Clock.Value = f.Clock.Value.AddSeconds(-1);
        Assert.Equal(409, (await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.Start(id, 2, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.Start(id, 3, default))).Status);
        f.Clock.Value = f.Clock.Value.AddMinutes(16);
        await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.Start(id, 2, default));
    }
    [Fact] public async Task TurnAndAttemptTimeoutsPersistWithoutClientSubmission()
    {
        await using var f = await Fixture.Create(); var id = await f.Published(); await f.Engine.Start(id, 2, default);
        f.Clock.Value = f.Clock.Value.AddSeconds(31);
        var next = Fixture.Json(await f.Engine.Resume(id, 2, default));
        Assert.Equal(2, next.GetProperty("turn").GetProperty("mainOrder").GetInt32());
        f.Clock.Value = f.Clock.Value.AddMinutes(16);
        var finished = Fixture.Json(await f.Engine.Resume(id, 2, default));
        Assert.Equal("Completed", finished.GetProperty("Status").GetString()); Assert.Equal(0, f.Llm.Calls);
    }
    [Fact] public async Task RejectWrongCourseOwnerEnrollmentPoolAndScheduleConflict()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(f.Input(), 2, null, default));
        await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(f.Input() with { StudentIds = [999] }, 1, null, default));
        await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(f.Input() with { MainQuestionCount = 10 }, 1, null, default));
        await f.Flow.Save(f.Input(), 1, null, default);
        Assert.Equal(409, (await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(f.Input(), 1, null, default))).Status);
    }
    [Fact] public async Task ConsecutiveCandidatesPreferDifferentQuestionsAndAssignmentsStayFixed()
    {
        await using var f = await Fixture.Create(); var id = await f.Published(f.Input(students: [2, 3]));
        var qs = await f.Db.ExamQuestions.ToListAsync();
        var groups = qs.GroupBy(q => q.CandidateId).ToArray();
        Assert.Empty(groups[0].Select(q => q.QuestionId).Intersect(groups[1].Select(q => q.QuestionId)));
        await f.Flow.Publish(id, 1, default); Assert.Equal(4, await f.Db.ExamQuestions.CountAsync());
    }
    [Fact] public async Task AudioAndTtsFailuresAreExplicitAndPersisted()
    {
        await using var f = await Fixture.Create(); var id = await f.Published();
        var state = Fixture.Json(await f.Engine.Start(id, 2, default)); var turn = state.GetProperty("turn").GetProperty("turnId").GetString()!;
        Assert.Equal(503, (await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.Speak(id, 2, turn, default))).Status);
        await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.Transcribe(id, 2, turn, Guid.NewGuid().ToString(), [1, 2], default));
        Assert.Contains(await f.Db.ExamLogs.ToListAsync(), l => l.Action == "ProviderError");
    }
    [Theory]
    [InlineData(0, 15, 1)] [InlineData(181, 15, 1)] [InlineData(15, 15, 0)]
    public void ValidateRejectsInvalidSlots(int slot, int window, int students)
    {
        var start = DateTimeOffset.UtcNow;
        Assert.Throws<WorkflowException>(() => WorkflowRules.Validate(new(1, "Test", null, start, start.AddMinutes(window), slot, 1, 1, 1, 30, "Random", Enumerable.Range(1, students).ToArray()), 3));
    }

    [Fact] public async Task StudentConflictAcrossDifferentLecturersAndCoursesIsRejected()
    {
        await using var f = await Fixture.Create(); await f.Flow.Save(f.Input(), 1, null, default);
        f.Db.Users.Add(new User { UserId = 4, RoleId = 1, Email = "other@test.invalid", FullName = "Other Lecturer" });
        f.Db.Courses.Add(new Course { CourseId = 2, LecturerId = 4, CourseCode = "C2", CourseName = "Other" });
        f.Db.AcademicClasses.Add(new AcademicClass { ClassId = 2, CourseId = 2, LecturerId = 4, ClassCode = "C2", ClassName = "Other" });
        f.Db.ClassStudents.Add(new ClassStudent { ClassId = 2, StudentId = 2 });
        f.Db.Questions.Add(new Question { CourseId = 2, CreatedBy = 4, QuestionText = "Other question", Status = "Approved" });
        await f.Db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Save(f.Input(main: 1) with { CourseId = 2, ClassId = 2 }, 4, null, default))).Status);
    }

    [Fact] public async Task SmallPoolWarnsWithoutDuplicatesWithinCandidateAndAdaptiveBalancesDifficulty()
    {
        await using var f = await Fixture.Create(); var id = (await f.Flow.Save(f.Input(main: 4) with { Strategy = "Adaptive" }, 1, null, default)).ExamId;
        var warnings = await f.Flow.Publish(id, 1, default);
        Assert.NotEmpty(warnings);
        var questions = await f.Db.ExamQuestions.Include(q => q.Question).ToListAsync();
        Assert.Equal(4, questions.Select(q => q.QuestionId).Distinct().Count());
        Assert.Equal(3, questions.Select(q => q.Question!.Difficulty).Distinct().Count());
    }

    [Fact] public async Task FollowupTimeoutMovesOnAndSweepMarksAbsentAndCompletesExam()
    {
        await using var f = await Fixture.Create(); var id = await f.Published(f.Input(students: [2, 3]));
        await f.Engine.Start(id, 2, default); await f.Submit(id);
        f.Clock.Value = f.Clock.Value.AddSeconds(31);
        var next = Fixture.Json(await f.Engine.Resume(id, 2, default));
        Assert.Equal(2, next.GetProperty("turn").GetProperty("mainOrder").GetInt32());
        Assert.NotNull((await f.Db.FollowUpQuestions.FirstAsync()).AnsweredAt);
        f.Clock.Value = f.Clock.Value.AddHours(1); await f.Engine.Sweep(id, default);
        Assert.Equal("Completed", (await f.Db.ExamSessions.FindAsync(id))!.Status);
        Assert.Contains(await f.Db.ExamCandidates.ToListAsync(), c => c.StudentId == 3 && c.Status == "Absent");
    }

    [Fact] public async Task ValidAudioFailureAndSuccessfulChunkRetryPersistAndResume()
    {
        await using var f = await Fixture.Create(); var id = await f.Published();
        var state = Fixture.Json(await f.Engine.Start(id, 2, default)); var turn = state.GetProperty("turn").GetProperty("turnId").GetString()!;
        using var buffer = new MemoryStream(); using (var writer = new BinaryWriter(buffer, System.Text.Encoding.ASCII, true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + 3200); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(3200); writer.Write(new byte[3200]);
        }
        var chunk = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<WorkflowException>(() => f.Engine.Transcribe(id, 2, turn, chunk, buffer.ToArray(), default));
        Assert.Contains(await f.Db.ExamLogs.ToListAsync(), l => l.Action == "ProviderError");
        f.Speech.Success = true;
        await f.Engine.Transcribe(id, 2, turn, chunk, buffer.ToArray(), default);
        await f.Engine.Transcribe(id, 2, turn, chunk, buffer.ToArray(), default);
        Assert.Equal(2, f.Speech.Calls); // One failed provider request, one successful; duplicate was not called.
        f.Db.ChangeTracker.Clear();
        state = Fixture.Json(await f.Engine.Resume(id, 2, default));
        Assert.Equal("Bản ghi thử", state.GetProperty("turn").GetProperty("transcript").GetString());
        Assert.Null((await f.Db.Answers.SingleAsync()).FinishedAt);
    }

    [Fact] public async Task DraftEditPreservesLegacyScopePropertiesAndPublishRejectsExistingAssignment()
    {
        await using var f = await Fixture.Create();
        var future = f.Input() with { StartTime = f.Clock.Value.AddMinutes(1) };
        var exam = await f.Flow.Save(future, 1, null, default);
        exam.QuestionScopeJson = "{\"existingBankFilter\":\"keep\"}"; await f.Db.SaveChangesAsync();
        await f.Flow.Save(future with { Strategy = "Adaptive" }, 1, exam.ExamId, default);
        Assert.Contains("existingBankFilter", exam.QuestionScopeJson);
        var candidate = await f.Db.ExamCandidates.FirstAsync();
        f.Db.ExamQuestions.Add(new ExamQuestion { ExamId = exam.ExamId, CandidateId = candidate.CandidateId, QuestionId = 1, OrderNo = 1 }); await f.Db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<WorkflowException>(() => f.Flow.Publish(exam.ExamId, 1, default))).Status);
        Assert.Single(await f.Db.ExamQuestions.ToListAsync());
    }
}
