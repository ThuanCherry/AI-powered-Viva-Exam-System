using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AIVES.Api.Controllers;
using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using AIVES.Api.Hubs;
using AIVES.Api.Models;
using AIVES.Lecturer.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIVES.Workflow.Tests;

public class LayeringTests
{
    [Fact]
    public void ControllersAndHubCannotInjectPersistenceThroughConstructorsActionsOrFields()
    {
        var controllers = typeof(ExamsController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)).ToArray();
        Assert.True(controllers.Length >= 8);
        foreach (var type in controllers.Append(typeof(ExamHub)))
        {
            var dependencies = type.GetConstructors().SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters().Select(p => p.ParameterType)))
                .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Select(f => f.FieldType));
            Assert.All(dependencies, dependency => Assert.False(IsPersistence(dependency), $"{type.Name} depends on {dependency.Name}"));
        }
    }

    private static bool IsPersistence(Type type) => typeof(DbContext).IsAssignableFrom(type)
        || type.Namespace == "AIVES.Api.Data"
        || type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
        || typeof(IQueryable).IsAssignableFrom(type)
        || type.GetGenericArguments().Any(IsPersistence);

    [Fact]
    public void HttpImplementationsDoNotContainEfQueriesOrSaveOperations()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "backend", "AIVES.Api", "Controllers")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var api = Path.Combine(directory.FullName, "backend", "AIVES.Api");
        var files = Directory.GetFiles(Path.Combine(api, "Controllers"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(api, "Hubs"), "*.cs"));
        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            foreach (var forbidden in new[] { "AivesDbContext", "using AIVES.Api.Data", "Microsoft.EntityFrameworkCore", "SaveChanges", "FromSql", "IQueryable" })
                Assert.False(code.Contains(forbidden, StringComparison.Ordinal), $"{file} contains persistence dependency {forbidden}");
        }
    }

    private static async Task<JsonElement> Call(LecturerSandbox s, HttpMethod method, string path, object? body = null,
        int user = 1, string role = "Lecturer", HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path);
        if (user != 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token(user, role));
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await s.Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{path}: {response.StatusCode}; {text}");
        if (string.IsNullOrWhiteSpace(text)) return default;
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        return json.TryGetProperty("data", out var data) ? data : json;
    }

    private static async Task<int> Course(LecturerSandbox s) => (await Call(s, HttpMethod.Post, "/api/lecturer/courses",
        new { courseCode = "LAYERS", courseName = "Phân tầng Monolithic" })).GetProperty("courseId").GetInt32();

    [Fact]
    public async Task CourseAndQuestionReadServicesKeepContractOwnershipAndValidation()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s);
        var list = await Call(s, HttpMethod.Get, "/api/Courses?lecturerId=4");
        Assert.Equal(course, list[0].GetProperty("courseId").GetInt32()); // Cannot spoof ownership via query.
        var detail = await Call(s, HttpMethod.Get, $"/api/Courses/{course}");
        Assert.Equal("LAYERS", detail.GetProperty("courseCode").GetString());
        await Call(s, HttpMethod.Get, $"/api/Courses/{course}", user: 4, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, "/api/Courses/99999", expected: HttpStatusCode.NotFound);
        var q = await Call(s, HttpMethod.Post, "/api/lecturer/questions", new { courseId = course,
            questionText = "Controller có nên dùng DbContext trực tiếp?", expectedAnswer = "Service xử lý nghiệp vụ",
            difficulty = "Medium", bloomLevel = "Understand", status = "Approved" });
        var id = q.GetProperty("questionId").GetInt32();
        var questions = await Call(s, HttpMethod.Get, $"/api/Questions?courseId={course}&q=Controller&difficulty=Medium&status=Approved&bloomLevel=Understand");
        Assert.Single(questions.EnumerateArray());
        Assert.Equal(id, questions[0].GetProperty("questionId").GetInt32());
        var question = await Call(s, HttpMethod.Get, $"/api/Questions/{id}");
        Assert.Equal("Service xử lý nghiệp vụ", question.GetProperty("expectedAnswer").GetString());
        Assert.Equal(JsonValueKind.Array, question.GetProperty("options").ValueKind);
        await Call(s, HttpMethod.Get, $"/api/Questions/{id}", user: 4, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, $"/api/Questions/{id}", user: 2, role: "Student", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, "/api/Questions?pageSize=101", expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Get, "/api/Questions/99999", expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserManagementServicesKeepAdminBoundaryAndResponseShape()
    {
        await using var s = await LecturerSandbox.Create();
        var students = await Call(s, HttpMethod.Get, "/api/Users?role=Admin");
        Assert.All(students.EnumerateArray(), student => {
            Assert.Equal("Student", student.GetProperty("role").GetString());
            Assert.False(student.TryGetProperty("passwordHash", out _));
        });
        var lecturers = await Call(s, HttpMethod.Get, "/api/Users?role=Lecturer", user: 100, role: "Admin");
        Assert.Equal(2, lecturers.GetArrayLength());
        await Call(s, HttpMethod.Put, "/api/Users/2", new { fullName = "Không được sửa" }, expected: HttpStatusCode.Forbidden);
        var updated = await Call(s, HttpMethod.Put, "/api/Users/2", new { fullName = "Sinh viên đã cập nhật", role = "Student", status = "Active" }, user: 100, role: "Admin");
        Assert.Equal("Sinh viên đã cập nhật", updated.GetProperty("fullName").GetString());
        Assert.Equal("Student", updated.GetProperty("role").GetString());
        var status = await Call(s, HttpMethod.Put, "/api/Users/2/status", user: 100, role: "Admin");
        Assert.Equal("Inactive", status.GetProperty("status").GetString());
        await Call(s, HttpMethod.Put, "/api/Users/99999/status", user: 100, role: "Admin", expected: HttpStatusCode.NotFound);
        await Call(s, HttpMethod.Put, "/api/Users/99999", new { fullName = "Missing" }, user: 100, role: "Admin", expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CourseAssignmentStillValidatesLecturerAndPreservesExistingWorkflow()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s);
        await Call(s, HttpMethod.Put, $"/api/Courses/{course}/assign-lecturer", new { lecturerId = 2 }, user: 100, role: "Admin", expected: HttpStatusCode.BadRequest);
        var assigned = await Call(s, HttpMethod.Put, $"/api/Courses/{course}/assign-lecturer", new { lecturerId = 4 }, user: 100, role: "Admin");
        Assert.Equal(4, assigned.GetProperty("lecturerId").GetInt32());
        Assert.Equal("Giảng viên khác", assigned.GetProperty("lecturerName").GetString());
        await Call(s, HttpMethod.Get, $"/api/Courses/{course}", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Post, "/api/lecturer/classes", new { courseId = course, classCode = "LAY1", className = "Lớp" }, user: 4);
        await Call(s, HttpMethod.Put, $"/api/Courses/{course}/assign-lecturer", new { lecturerId = 1 }, user: 100, role: "Admin", expected: HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DatabaseHealthIsAnApplicationServiceAndDoesNotInvokeProviders()
    {
        await using var s = await LecturerSandbox.Create();
        using var scope = s.App.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DatabaseHealthService>().CheckAsync(default));
        Assert.Equal(0, s.Ai.Calls); Assert.Equal(0, s.Network.Calls);
    }
}
