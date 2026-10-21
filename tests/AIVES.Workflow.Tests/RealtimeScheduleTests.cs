using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using AIVES.Api.Models;
using AIVES.Lecturer.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIVES.Workflow.Tests;

public class RealtimeScheduleTests
{
    private static async Task<LecturerSandbox> Setup()
    {
        var s = await LecturerSandbox.Create();
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        db.Users.Add(new User { UserId = 5, RoleId = 2, Email = "outside@test.invalid", FullName = "Outside" });
        db.Courses.Add(new Course { CourseId = 1, LecturerId = 1, CourseCode = "REALTIME", CourseName = "SignalR" });
        db.AcademicClasses.Add(new AcademicClass { ClassId = 1, CourseId = 1, LecturerId = 1, ClassCode = "RT1", ClassName = "Realtime" });
        db.ClassStudents.AddRange(new ClassStudent { ClassId = 1, StudentId = 2 }, new ClassStudent { ClassId = 1, StudentId = 3 });
        db.Questions.AddRange(Enumerable.Range(1, 5).Select(i => new Question { CourseId = 1, CreatedBy = 1, QuestionText = $"Manual {i}", Status = "Approved" }));
        await db.SaveChangesAsync(); return s;
    }
    private static ExamInput Input() => new(1, "Lịch ban đầu", null, DateTimeOffset.UtcNow.AddDays(2), null, 15, 2, 2, 1, 120, "Random", [2], 1);
    private static async Task<JsonElement> Call(LecturerSandbox s, HttpMethod method, string path, object? body = null, int user = 1, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token(user, user is 2 or 3 or 5 ? "Student" : "Lecturer"));
        if (body != null) request.Content = JsonContent.Create(body);
        using var result = await s.Http.SendAsync(request); var content = await result.Content.ReadAsStringAsync();
        Assert.True(result.StatusCode == expected, $"{method} {path}: {result.StatusCode} {content}");
        if (string.IsNullOrWhiteSpace(content)) return default;
        var json = JsonSerializer.Deserialize<JsonElement>(content); return json.TryGetProperty("data", out var data) ? data : json;
    }

    [Fact] public async Task RealSignalRPublishAndEditReachStudentsAndRefreshSchedulesWithoutAi()
    {
        await using var s = await Setup(); var input = Input();
        var id = (await Call(s, HttpMethod.Post, "/api/Exams", input)).GetProperty("examId").GetInt32();
        await using var student = await Socket.Connect(s, 2);
        await Call(s, HttpMethod.Post, $"/api/Exams/{id}/publish");
        Assert.Equal("published", (await student.Event()).GetProperty("reason").GetString());
        var before = await Call(s, HttpMethod.Get, $"/api/Exams/{id}");
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        var original = await db.ExamQuestions.Where(q => q.ExamId == id).Select(q => q.ExamQuestionId).ToArrayAsync();
        await using var addedStudent = await Socket.Connect(s, 3);
        var changed = input with { ExamName = "Lịch đã sửa", StartTime = input.StartTime.AddHours(1), TimePerStudent = 20, StudentIds = [3, 2], MaxFollowUpCount = 4 };
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", changed);
        Assert.Equal(id, (await student.Event()).GetProperty("examId").GetInt32());
        Assert.Equal("updated", (await addedStudent.Event()).GetProperty("reason").GetString());
        var mine = await Call(s, HttpMethod.Get, "/api/schedule/mine", user: 2);
        Assert.Equal("Lịch đã sửa", mine[0].GetProperty("examName").GetString());
        Assert.Equal(changed.StartTime.AddMinutes(20), mine[0].GetProperty("scheduledTime").GetDateTimeOffset());
        Assert.Single((await Call(s, HttpMethod.Get, "/api/schedule/mine", user: 3)).EnumerateArray());
        var after = await Call(s, HttpMethod.Get, $"/api/Exams/{id}"); Assert.Equal("Scheduled", after.GetProperty("status").GetString());
        Assert.Equal(changed.StartTime.AddMinutes(40), after.GetProperty("endTime").GetDateTimeOffset());
        Assert.Equal(2, await db.ExamQuestions.CountAsync(q => original.Contains(q.ExamQuestionId))); Assert.Equal(4, await db.ExamQuestions.CountAsync(q => q.ExamId == id));
        Assert.Equal(0, s.Ai.Calls); Assert.Equal(0, s.Network.Calls);
    }

    [Fact] public async Task RemovedStudentAndOwnerReceiveUpdateWhileOutsiderReceivesNothing()
    {
        await using var s = await Setup(); var input = Input(); var id = (await Call(s, HttpMethod.Post, "/api/Exams", input)).GetProperty("examId").GetInt32();
        await Call(s, HttpMethod.Post, $"/api/Exams/{id}/publish");
        await using var removed = await Socket.Connect(s, 2); await using var teacher = await Socket.Connect(s, 1); await using var outsider = await Socket.Connect(s, 5);
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input with { StudentIds = [3] });
        Assert.Equal(id, (await removed.Event()).GetProperty("examId").GetInt32()); Assert.Equal(id, (await teacher.Event()).GetProperty("examId").GetInt32());
        Assert.Empty((await Call(s, HttpMethod.Get, "/api/schedule/mine", user: 2)).EnumerateArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => outsider.Event(250));
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        Assert.Equal(4, await db.ExamQuestions.CountAsync(q => q.ExamId == id)); // Old assignment retained, never deleted.
    }

    [Fact] public async Task CancellationAndReconnectionAreDeliveredWithCurrentSchedule()
    {
        await using var s = await Setup(); var input = Input(); var id = (await Call(s, HttpMethod.Post, "/api/Exams", input)).GetProperty("examId").GetInt32();
        await Call(s, HttpMethod.Post, $"/api/Exams/{id}/publish");
        await using var student = await Socket.Connect(s, 2);
        await Call(s, HttpMethod.Post, $"/api/Exams/{id}/cancel"); Assert.Equal("cancelled", (await student.Event()).GetProperty("reason").GetString());
        Assert.Empty((await Call(s, HttpMethod.Get, "/api/schedule/mine", user: 2)).EnumerateArray());
        await using var reconnected = await Socket.Connect(s, 2);
        var next = (await Call(s, HttpMethod.Post, "/api/Exams", input with { StartTime = input.StartTime.AddDays(1) })).GetProperty("examId").GetInt32();
        await Call(s, HttpMethod.Post, $"/api/Exams/{next}/publish"); Assert.Equal(next, (await reconnected.Event()).GetProperty("examId").GetInt32());
    }

    [Fact] public async Task PublishedAssignmentsAreProtectedAndStartedScheduleCannotBeChanged()
    {
        await using var s = await Setup(); var input = Input(); var id = (await Call(s, HttpMethod.Post, "/api/Exams", input)).GetProperty("examId").GetInt32();
        await Call(s, HttpMethod.Post, $"/api/Exams/{id}/publish");
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input with { MainQuestionCount = 3 }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input with { Strategy = "Adaptive" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input, user: 4, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input with { StartTime = DateTimeOffset.UtcNow.AddMinutes(-1) }, expected: HttpStatusCode.BadRequest);
        using (var scope = s.App.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>(); var e = await db.ExamSessions.FindAsync(id); e!.StartTime = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); }
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input, expected: HttpStatusCode.Conflict);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void ProviderDiagnosticsExplainMissingConfigurationWithoutExposingSecret(bool configured)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Provider"] = "Gemini", ["Speech:Provider"] = "Gemini", ["AI:Model"] = "test-model", ["AI:ApiKey"] = configured ? "fake-local-test-key" : null }).Build();
        var view = JsonSerializer.SerializeToElement(new ProviderStatus(config).View);
        Assert.Equal(configured ? "Configured" : "NotConfigured", view.GetProperty("llm").GetString());
        Assert.DoesNotContain("fake-local-test-key", view.ToString());
        if (!configured) Assert.Contains("AI:ApiKey", view.GetProperty("missing").ToString());
    }

    [Fact] public async Task EnrollmentChangesNotifyStudentWithoutNeedingExamMembership()
    {
        await using var s = await Setup(); await using var student = await Socket.Connect(s, 5);
        await Call(s, HttpMethod.Post, "/api/lecturer/classes/1/students/5");
        Assert.Equal("enrollment", (await student.Event()).GetProperty("reason").GetString());
        Assert.Single((await Call(s, HttpMethod.Get, "/api/classes", user: 5)).EnumerateArray());
        await Call(s, HttpMethod.Delete, "/api/lecturer/classes/1/students/5");
        Assert.Equal("enrollment", (await student.Event()).GetProperty("reason").GetString());
        Assert.Empty((await Call(s, HttpMethod.Get, "/api/classes", user: 5)).EnumerateArray());
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:5174")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://127.0.0.1:5174")]
    public async Task BrowserSignalRNegotiationAllowsAuthenticatedFrontendOrigin(string origin)
    {
        await using var s = await Setup(); using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/exam/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token(2, "Student")); request.Headers.Add("Origin", origin);
        using var response = await s.Http.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact] public void ProductionDoesNotAutomaticallyTrustDevelopmentOrigins()
    {
        Assert.Equal(new[] { "https://aives.example" }, FrontendOrigins.Resolve(["https://aives.example"], false));
    }

    [Fact] public async Task EditingPublishedScheduleStillChecksConflictsAndKeepsOriginalOnFailure()
    {
        await using var s = await Setup(); var input = Input();
        var id = (await Call(s, HttpMethod.Post, "/api/Exams", input)).GetProperty("examId").GetInt32(); await Call(s, HttpMethod.Post, $"/api/Exams/{id}/publish");
        await Call(s, HttpMethod.Post, "/api/Exams", input with { StartTime = input.StartTime.AddHours(2) });
        await Call(s, HttpMethod.Put, $"/api/Exams/{id}", input with { StartTime = input.StartTime.AddHours(2) }, expected: HttpStatusCode.Conflict);
        var unchanged = await Call(s, HttpMethod.Get, $"/api/Exams/{id}"); Assert.Equal(input.StartTime, unchanged.GetProperty("startTime").GetDateTimeOffset());
        Assert.Equal("Scheduled", unchanged.GetProperty("status").GetString());
    }

    private sealed class Socket : IAsyncDisposable
    {
        private readonly ClientWebSocket ws = new(); private readonly Queue<JsonElement> messages = new();
        public static async Task<Socket> Connect(LecturerSandbox s, int user)
        {
            var client = new Socket(); var uri = new UriBuilder(s.Http.BaseAddress!) { Scheme = "ws", Path = "/hubs/exam", Query = "access_token=" + s.Token(user, user is 1 or 4 ? "Lecturer" : "Student") }.Uri;
            using var timeout = new CancellationTokenSource(5000); await client.ws.ConnectAsync(uri, timeout.Token);
            await client.Send(new { protocol = "json", version = 1 }); await client.Read(timeout.Token);
            await client.Send(new { type = 1, invocationId = "ready", target = "LeaveExam", arguments = new[] { 0 } });
            while (true) { var ready = await client.Read(timeout.Token); if (ready.TryGetProperty("type", out var type) && type.GetInt32() == 3) break; }
            return client;
        }
        private Task Send(object value) => ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + '\u001e')), WebSocketMessageType.Text, true, CancellationToken.None);
        private async Task<JsonElement> Read(CancellationToken ct)
        {
            while (messages.Count == 0)
            {
                using var stream = new MemoryStream(); var buffer = new byte[8192]; WebSocketReceiveResult result;
                do { result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct); if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("SignalR closed"); stream.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
                foreach (var item in Encoding.UTF8.GetString(stream.ToArray()).Split('\u001e', StringSplitOptions.RemoveEmptyEntries)) messages.Enqueue(JsonSerializer.Deserialize<JsonElement>(item));
            }
            return messages.Dequeue();
        }
        public async Task<JsonElement> Event(int timeout = 5000)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (true) { var message = await Read(cts.Token); if (message.TryGetProperty("target", out var target) && target.GetString() == "ScheduleChanged") return message.GetProperty("arguments")[0]; }
        }
        public ValueTask DisposeAsync() { ws.Abort(); ws.Dispose(); return ValueTask.CompletedTask; }
    }
}
