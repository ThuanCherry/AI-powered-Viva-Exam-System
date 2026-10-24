using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIVES.Api.Data;
using AIVES.Lecturer.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIVES.Workflow.Tests;

public class AdminWorkflowTests
{
    private const string Password = "Admin-Test-Only-123!";
    private static async Task<JsonElement> Call(LecturerSandbox s, HttpMethod method, string path, object? body = null,
        string? token = null, HttpStatusCode expected = HttpStatusCode.OK, bool anonymous = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? s.Token(100, "Admin"));
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await s.Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{method} {path}: {response.StatusCode}; {text}");
        if (string.IsNullOrWhiteSpace(text)) return default;
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        return json.TryGetProperty("data", out var data) ? data : json;
    }

    [Fact]
    public async Task CreatePersistLoginEditLockAndUnlockWithRealHttp()
    {
        await using var s = await LecturerSandbox.Create();
        var created = await Call(s, HttpMethod.Post, "/api/Admin/users", new { fullName = "  Giảng viên mới  ", email = "NEW@TEST.INVALID", password = Password, role = "Lecturer" });
        var id = created.GetProperty("userId").GetInt32();
        Assert.Equal("new@test.invalid", created.GetProperty("email").GetString());
        Assert.False(created.TryGetProperty("passwordHash", out _));
        using (var scope = s.App.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AivesDbContext>().Users.SingleAsync(u => u.UserId == id);
            Assert.NotEqual(Password, user.PasswordHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash));
        }
        var login = await Call(s, HttpMethod.Post, "/api/Users/login", new { email = "new@test.invalid", password = Password }, anonymous: true);
        var token = login.GetProperty("token").GetString()!;
        await Call(s, HttpMethod.Get, "/api/lecturer/dashboard", token: token);
        await Call(s, HttpMethod.Put, $"/api/Users/{id}", new { fullName = "Tên đã sửa", role = "Lecturer" });
        var list = await Call(s, HttpMethod.Get, "/api/Users");
        Assert.Contains(list.EnumerateArray(), u => u.GetProperty("userId").GetInt32() == id && u.GetProperty("fullName").GetString() == "Tên đã sửa");
        await Call(s, HttpMethod.Put, $"/api/Users/{id}/status");
        await Call(s, HttpMethod.Get, "/api/lecturer/dashboard", token: token, expected: HttpStatusCode.Unauthorized);
        await Call(s, HttpMethod.Post, "/api/Users/login", new { email = "new@test.invalid", password = Password }, anonymous: true, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, $"/api/Users/{id}/status");
        var unlocked = await Call(s, HttpMethod.Post, "/api/Users/login", new { email = "new@test.invalid", password = Password }, anonymous: true);
        Assert.Equal("Lecturer", unlocked.GetProperty("role").GetString());
    }

    [Fact]
    public async Task DuplicateInvalidInputAndProtectedAdminsCannotBypassUi()
    {
        await using var s = await LecturerSandbox.Create();
        await Call(s, HttpMethod.Post, "/api/Admin/users", new { fullName = "Name", email = "STUDENT1@LECTURER-TEST.INVALID", password = Password, role = "Student" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Post, "/api/Admin/users", new { fullName = "Name", email = "bad-email", password = Password, role = "Student" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Post, "/api/Admin/users", new { fullName = "Name", email = "valid@test.invalid", password = "123456", role = "Student" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Post, "/api/Admin/users", new { fullName = "Name", email = "valid@test.invalid", password = Password, role = "SuperAdmin" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, "/api/Users/2", new { status = "bad" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, "/api/Users/2", new { role = "bad" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, "/api/Users/2", new { fullName = " " }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, "/api/Users/100/status", expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Put, "/api/Users/100", new { role = "Student" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Put, "/api/Users/100", new { status = "Inactive" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Get, "/api/Admin/settings");
    }

    [Theory]
    [InlineData(0, "", HttpStatusCode.Unauthorized)]
    [InlineData(1, "Lecturer", HttpStatusCode.Forbidden)]
    [InlineData(2, "Student", HttpStatusCode.Forbidden)]
    [InlineData(1, "Admin", HttpStatusCode.Unauthorized)]
    public async Task AdminEndpointsRequireCurrentAdminRole(int user, string role, HttpStatusCode expected)
    {
        await using var s = await LecturerSandbox.Create();
        await Call(s, HttpMethod.Post, "/api/Admin/users", new { fullName = "No", email = "no@test.invalid", password = Password, role = "Admin" },
            token: s.Token(user, role), anonymous: user == 0, expected: expected);
        await Call(s, HttpMethod.Get, "/api/Admin/settings", token: s.Token(user, role), anonymous: user == 0, expected: expected);
    }

    [Fact]
    public async Task RoleChangesInvalidateOldTokenAndRespectAcademicLinks()
    {
        await using var s = await LecturerSandbox.Create();
        var token = s.Token(4, "Lecturer");
        await Call(s, HttpMethod.Put, "/api/Users/4", new { role = "Student" });
        await Call(s, HttpMethod.Get, "/api/lecturer/dashboard", token: token, expected: HttpStatusCode.Unauthorized);
        var course = await Call(s, HttpMethod.Post, "/api/lecturer/courses", new { courseCode = "ADMINQA", courseName = "Test" }, token: s.Token());
        var id = course.GetProperty("courseId").GetInt32();
        await Call(s, HttpMethod.Put, "/api/Users/1", new { role = "Student" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Put, $"/api/Courses/{id}/assign-lecturer", new { lecturerId = 4 }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, "/api/Users/4", new { role = "Lecturer" });
        await Call(s, HttpMethod.Put, "/api/Users/4/status");
        await Call(s, HttpMethod.Put, $"/api/Courses/{id}/assign-lecturer", new { lecturerId = 4 }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Put, "/api/Users/4/status");
        var assigned = await Call(s, HttpMethod.Put, $"/api/Courses/{id}/assign-lecturer", new { lecturerId = 4 });
        Assert.Equal(4, assigned.GetProperty("lecturerId").GetInt32());
        await Call(s, HttpMethod.Get, $"/api/Courses/{id}", token: s.Token(), expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, $"/api/Courses/{id}", token: s.Token(4));
    }

    [Fact]
    public async Task SettingsReportConfigurationOnlyWithoutSecretsOrProviderCalls()
    {
        await using var s = await LecturerSandbox.Create();
        var settings = await Call(s, HttpMethod.Get, "/api/Admin/settings");
        Assert.True(settings.GetProperty("readOnly").GetBoolean());
        Assert.Equal("NotConfigured", settings.GetProperty("providers").GetProperty("llm").GetString());
        Assert.False(settings.TryGetProperty("apiKey", out _));
        Assert.Equal(0, s.Ai.Calls); Assert.Equal(0, s.Network.Calls);
        // Removed fake write action must not appear to succeed.
        await Call(s, HttpMethod.Put, "/api/Admin/settings", new { ttsSpeedRate = 1.5 }, expected: HttpStatusCode.MethodNotAllowed);
    }
}
