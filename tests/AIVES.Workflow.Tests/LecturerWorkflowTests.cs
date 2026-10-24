using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Models;
using AIVES.Lecturer.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIVES.Workflow.Tests;

// Only this test class is selected for the Lecturer task; no provider probes or Live tests run.
public class LecturerWorkflowTests
{
    private static async Task<JsonElement> Call(LecturerSandbox s, HttpMethod method, string path, object? body = null, int user = 1, string role = "Lecturer", HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path);
        if (user != 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token(user, role));
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await s.Http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{method} {path}: expected {(int)expected}, got {(int)response.StatusCode}: {content}");
        if (string.IsNullOrEmpty(content) || !response.Content.Headers.ContentType?.MediaType?.Contains("json") == true) return default;
        var json = JsonSerializer.Deserialize<JsonElement>(content); return json.TryGetProperty("data", out var data) ? data : json;
    }
    private static async Task<int> Course(LecturerSandbox s, string code = "SWD", int user = 1) => (await Call(s, HttpMethod.Post, "/api/lecturer/courses", new { courseCode = code, courseName = "Thiết kế phần mềm", description = "Kiểm thử không AI" }, user)).GetProperty("courseId").GetInt32();
    private static async Task<int> Class(LecturerSandbox s, int course, string code = "SE01") => (await Call(s, HttpMethod.Post, "/api/lecturer/classes", new { courseId = course, classCode = code, className = "Lớp học phần" })).GetProperty("classId").GetInt32();
    private static async Task<int> Question(LecturerSandbox s, int course, string status = "Approved") => (await Call(s, HttpMethod.Post, "/api/lecturer/questions", QuestionBody(course, status))).GetProperty("questionId").GetInt32();
    private static object QuestionBody(int course, string status = "Approved") => new { courseId = course, questionText = "Giải thích transaction.", expectedAnswer = "Các tính chất ACID", difficulty = "Medium", bloomLevel = "Understand", status };
    private static object Exam(int course, int section, int[]? students = null, int count = 2) => new {
        courseId = course, classId = section, examName = "Vấn đáp lớp SE", startTime = DateTimeOffset.UtcNow.AddHours(1), endTime = DateTimeOffset.UtcNow.AddHours(2), timePerStudent = 15,
        mainQuestionCount = count, maxFollowUpCount = 2, maxFollowUpsPerQuestion = 1, secondsPerQuestion = 120, strategy = "Adaptive", studentIds = students ?? [2] };
    private static async Task<HttpResponseMessage> Upload(LecturerSandbox s, int course, byte[] bytes, string name, int user = 1)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/lecturer/courses/{course}/documents"); request.Headers.Authorization = new("Bearer", s.Token(user));
        using var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(bytes), "file", name); request.Content = body;
        return await s.Http.SendAsync(request);
    }

    [Fact] public async Task FullLecturerToStudentScheduleFlowWithoutAnyAiOrRealDatabaseWrite()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s); var section = await Class(s, course);
        var search = await Call(s, HttpMethod.Get, "/api/lecturer/students/search?q=student"); Assert.Equal(2, search.GetArrayLength());
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/2");
        var imported = await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/import", new { csv = "email\nstudent2@lecturer-test.invalid" }); Assert.Equal(1, imported.GetProperty("added").GetInt32());
        await Question(s, course); await Question(s, course);
        var classes = await Call(s, HttpMethod.Get, $"/api/classes?courseId={course}"); Assert.Single(classes.EnumerateArray());
        using var upload = await Upload(s, course, Encoding.ASCII.GetBytes("%PDF-1.4\nTest only\n%%EOF"), "../../private.pdf"); Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var doc = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("documentId").GetInt32();
        var roster = await Call(s, HttpMethod.Get, $"/api/lecturer/classes/{section}/students"); Assert.Equal(2, roster.GetArrayLength());
        var draft = await Call(s, HttpMethod.Post, "/api/Exams", Exam(course, section)); var exam = draft.GetProperty("examId").GetInt32();
        var preview = await Call(s, HttpMethod.Get, $"/api/Exams/{exam}"); Assert.Equal(section, preview.GetProperty("classId").GetInt32()); Assert.Single(preview.GetProperty("candidates").EnumerateArray());
        await Call(s, HttpMethod.Put, $"/api/Exams/{exam}", Exam(course, section));
        await Call(s, HttpMethod.Post, $"/api/Exams/{exam}/publish");
        var assigned = await Call(s, HttpMethod.Get, $"/api/Exams/{exam}");
        Assert.Equal(2, assigned.GetProperty("candidates")[0].GetProperty("questions").GetArrayLength());
        var studentView = await Call(s, HttpMethod.Get, $"/api/Exams/{exam}", user: 2, role: "Student");
        Assert.Equal(JsonValueKind.Null, studentView.GetProperty("candidates")[0].GetProperty("questions").ValueKind);
        var schedule = await Call(s, HttpMethod.Get, "/api/schedule/mine", user: 2, role: "Student"); Assert.Single(schedule.EnumerateArray());
        var otherSchedule = await Call(s, HttpMethod.Get, "/api/schedule/mine", user: 3, role: "Student"); Assert.Empty(otherSchedule.EnumerateArray());
        await Call(s, HttpMethod.Get, $"/api/Exams/{exam}", user: 3, role: "Student", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Delete, $"/api/lecturer/classes/{section}/students/2", expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Get, $"/api/documents/{doc}/download", user: 2, role: "Student");
        await Call(s, HttpMethod.Get, $"/api/documents/{doc}/download", user: 4, expected: HttpStatusCode.Forbidden);
        var dashboard = await Call(s, HttpMethod.Get, "/api/lecturer/dashboard"); Assert.Equal(1, dashboard.GetProperty("courseCount").GetInt32()); Assert.Equal(2, dashboard.GetProperty("studentCount").GetInt32());
        await Call(s, HttpMethod.Post, $"/api/Exams/{exam}/cancel");
        await Call(s, HttpMethod.Delete, $"/api/lecturer/classes/{section}/students/2");
        Assert.Equal(0, s.Ai.Calls); Assert.Equal(0, s.Network.Calls);
    }

    [Fact] public async Task AuthenticationOwnershipAndClassIsolationAreEnforcedByBackend()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s); var a = await Class(s, course); var b = await Class(s, course, "SE02");
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{a}/students/2"); await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{b}/students/3"); await Question(s, course);
        await Call(s, HttpMethod.Get, "/api/lecturer/dashboard", user: 0, expected: HttpStatusCode.Unauthorized);
        await Call(s, HttpMethod.Get, "/api/Users", user: 0, expected: HttpStatusCode.Unauthorized);
        await Call(s, HttpMethod.Put, "/api/Users/2", new { role = "Admin" }, user: 0, expected: HttpStatusCode.Unauthorized);
        await Call(s, HttpMethod.Put, "/api/Users/2", new { role = "Admin" }, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Put, "/api/Users/2/status", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Put, $"/api/Courses/{course}/assign-lecturer", new { lecturerId = 4 }, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, "/api/lecturer/dashboard", user: 2, role: "Student", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, "/api/lecturer/dashboard", user: 100, role: "Admin", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Put, $"/api/lecturer/courses/{course}", new { courseCode = "X", courseName = "Other" }, user: 4, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{a}/students/3", user: 4, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Get, $"/api/lecturer/classes/{a}/students", user: 2, role: "Student", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Post, "/api/Exams", Exam(course, a, [3], 1), expected: HttpStatusCode.BadRequest);
        var mine = await Call(s, HttpMethod.Get, "/api/classes", user: 2, role: "Student"); Assert.Equal(a, mine[0].GetProperty("classId").GetInt32()); Assert.Single(mine.EnumerateArray());
        var other = await Call(s, HttpMethod.Get, "/api/classes", user: 4); Assert.Empty(other.EnumerateArray());
        await Call(s, HttpMethod.Get, "/api/Questions", user: 2, role: "Student", expected: HttpStatusCode.Forbidden);
    }

    [Fact] public async Task CourseClassCodesAndQuestionValidationCannotBeBypassed()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s); var section = await Class(s, course);
        await Call(s, HttpMethod.Post, "/api/lecturer/courses", new { courseCode = "swd", courseName = "Duplicate" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Post, "/api/lecturer/classes", new { courseId = course, classCode = "se01", className = "Duplicate" }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Post, "/api/lecturer/classes", new { courseId = course, classCode = "invalid code", className = "X" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Post, "/api/lecturer/questions", new { courseId = course, questionText = "", difficulty = "Easy", bloomLevel = "Remember", status = "Approved" }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Post, "/api/Exams", Exam(course, section, count: 1), expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/1", expected: HttpStatusCode.BadRequest);
    }

    [Fact] public async Task CsvImportIsAtomicIdempotentAndDoesNotCreateAccounts()
    {
        await using var s = await LecturerSandbox.Create(); var c = await Course(s); var section = await Class(s, c);
        var bad = await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/import", new { csv = "email\nstudent1@lecturer-test.invalid\nmissing@example.invalid" }); Assert.NotEmpty(bad.GetProperty("errors").EnumerateArray());
        Assert.Empty((await Call(s, HttpMethod.Get, $"/api/lecturer/classes/{section}/students")).EnumerateArray());
        var valid = new { csv = "email\nstudent1@lecturer-test.invalid\nstudent2@lecturer-test.invalid" };
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/import", valid);
        var retry = await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/import", valid); Assert.Equal(0, retry.GetProperty("added").GetInt32()); Assert.Equal(2, retry.GetProperty("alreadyEnrolled").GetInt32());
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{section}/students/2"); Assert.Equal(2, (await Call(s, HttpMethod.Get, $"/api/lecturer/classes/{section}/students")).GetArrayLength());
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>(); Assert.Equal(new[] { 1, 2, 3, 4, 100 }, await db.Users.OrderBy(u => u.UserId).Select(u => u.UserId).ToArrayAsync());
    }

    [Fact] public async Task DocumentsArePrivateValidatedAndArchivedWithoutDeletingBytes()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s);
        using var bad = await Upload(s, course, Encoding.ASCII.GetBytes("<html>bad</html>"), "fake.pdf"); Assert.Equal(HttpStatusCode.UnsupportedMediaType, bad.StatusCode);
        using var wrongType = await Upload(s, course, [1, 2, 3], "code.exe"); Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        using var big = await Upload(s, course, new byte[10 * 1024 * 1024 + 1], "large.pdf"); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, big.StatusCode);
        using var zipped = new MemoryStream(); using (var zip = new ZipArchive(zipped, ZipArchiveMode.Create, true)) { using (var a = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open())) a.Write("test"); using (var b = new StreamWriter(zip.CreateEntry("word/document.xml").Open())) b.Write("test"); }
        using var response = await Upload(s, course, zipped.ToArray(), "valid.docx"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("documentId").GetInt32();
        await Call(s, HttpMethod.Get, $"/api/documents/{doc}/download", user: 2, role: "Student", expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Delete, $"/api/lecturer/documents/{doc}", user: 4, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Delete, $"/api/lecturer/documents/{doc}");
        Assert.Empty((await Call(s, HttpMethod.Get, $"/api/documents?courseId={course}")).EnumerateArray());
        Assert.Single(Directory.GetFiles(s.Storage));
        await Call(s, HttpMethod.Get, $"/api/documents/{doc}/download", expected: HttpStatusCode.NotFound);
    }

    [Fact] public async Task UsedQuestionsCannotBeRewrittenAndArchivingPreservesAssignments()
    {
        await using var s = await LecturerSandbox.Create(); var c = await Course(s); var cl = await Class(s, c); await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{cl}/students/2");
        var q = await Question(s, c, "Draft"); await Call(s, HttpMethod.Put, $"/api/lecturer/questions/{q}", QuestionBody(c));
        var exam = (await Call(s, HttpMethod.Post, "/api/Exams", Exam(c, cl, count: 1))).GetProperty("examId").GetInt32(); await Call(s, HttpMethod.Post, $"/api/Exams/{exam}/publish");
        await Call(s, HttpMethod.Put, $"/api/lecturer/questions/{q}", QuestionBody(c), expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Delete, $"/api/lecturer/questions/{q}");
        Assert.Empty((await Call(s, HttpMethod.Get, $"/api/Questions?courseId={c}")).EnumerateArray());
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>(); Assert.Equal(1, await db.ExamQuestions.CountAsync()); Assert.Equal("Archived", (await db.Questions.FindAsync(q))!.Status);
    }

    [Fact] public async Task NewExamRequiresClassAndRejectsWrongCourseAndScheduleOverlap()
    {
        await using var s = await LecturerSandbox.Create(); var c = await Course(s); var cl = await Class(s, c); await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{cl}/students/2"); await Question(s, c);
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var input = new ExamInput(c, "Test", null, start, start.AddHours(1), 15, 1, 1, 1, 120, "Random", [2]);
        await Call(s, HttpMethod.Post, "/api/Exams", input, expected: HttpStatusCode.BadRequest);
        var second = await Course(s, "OTHER"); await Call(s, HttpMethod.Post, "/api/Exams", input with { CourseId = second, ClassId = cl }, expected: HttpStatusCode.Forbidden);
        await Call(s, HttpMethod.Post, "/api/Exams", input with { ClassId = cl });
        await Call(s, HttpMethod.Post, "/api/Exams", input with { ClassId = cl }, expected: HttpStatusCode.Conflict);
        await Call(s, HttpMethod.Post, "/api/Exams", input with { ClassId = cl, TimePerStudent = 0 }, expected: HttpStatusCode.BadRequest);
        await Call(s, HttpMethod.Post, "/api/Exams", input with { ClassId = cl, MainQuestionCount = 10 }, expected: HttpStatusCode.BadRequest);
    }

    [Fact] public async Task StudentSlotConflictAcrossLecturersIsRejectedAndEnrollmentCanBeRemovedSafely()
    {
        await using var s = await LecturerSandbox.Create(); var c1 = await Course(s); var cl1 = await Class(s, c1); await Question(s, c1); await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{cl1}/students/2");
        await Call(s, HttpMethod.Post, "/api/Exams", Exam(c1, cl1, count: 1));
        var c2 = await Course(s, "OTHER", 4);
        var cl2 = (await Call(s, HttpMethod.Post, "/api/lecturer/classes", new { courseId = c2, classCode = "OTHER-CLASS", className = "Lớp khác" }, user: 4)).GetProperty("classId").GetInt32();
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{cl2}/students/2", user: 4);
        await Call(s, HttpMethod.Post, "/api/lecturer/questions", QuestionBody(c2), user: 4);
        await Call(s, HttpMethod.Post, "/api/Exams", Exam(c2, cl2, count: 1), user: 4, expected: HttpStatusCode.Conflict);
        // The unrelated draft in course 1 must not block leaving class in course 2.
        await Call(s, HttpMethod.Delete, $"/api/lecturer/classes/{cl2}/students/2", user: 4);
        var mine = await Call(s, HttpMethod.Get, "/api/classes", user: 2, role: "Student"); Assert.Single(mine.EnumerateArray());
    }

    [Fact] public async Task CsvRejectsDuplicatesInactiveAccountsAndWrongHeaderWithoutPartialWrite()
    {
        await using var s = await LecturerSandbox.Create(); var course = await Course(s); var cl = await Class(s, course);
        using (var scope = s.App.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>(); (await db.Users.FindAsync(3))!.Status = "Inactive"; await db.SaveChangesAsync(); }
        await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{cl}/students/import", new { csv = "name\nstudent1@lecturer-test.invalid" }, expected: HttpStatusCode.BadRequest);
        foreach (var csv in new[] { "email\nstudent1@lecturer-test.invalid\nstudent1@lecturer-test.invalid", "email\nstudent2@lecturer-test.invalid", "email\nlecturer@lecturer-test.invalid" })
        {
            var result = await Call(s, HttpMethod.Post, $"/api/lecturer/classes/{cl}/students/import", new { csv }); Assert.NotEmpty(result.GetProperty("errors").EnumerateArray());
            Assert.Empty((await Call(s, HttpMethod.Get, $"/api/lecturer/classes/{cl}/students")).EnumerateArray());
        }
    }

    [Fact] public async Task DocumentDownloadRejectsLegacyTraversalAndCourseCatalogHidesOtherOwners()
    {
        await using var s = await LecturerSandbox.Create(); var c = await Course(s); await Course(s, "HIDDEN", 4);
        Assert.Single((await Call(s, HttpMethod.Get, "/api/Courses")).EnumerateArray());
        Assert.Empty((await Call(s, HttpMethod.Get, "/api/Courses", user: 2, role: "Student")).EnumerateArray());
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        var doc = new Document { CourseId = c, UploadedById = 1, FileName = "private.pdf", ContentType = "application/pdf", StoragePath = "../../outside.pdf", Status = "Uploaded", CreatedAt = DateTime.UtcNow };
        db.Documents.Add(doc); await db.SaveChangesAsync();
        await Call(s, HttpMethod.Get, $"/api/documents/{doc.DocumentId}/download", expected: HttpStatusCode.NotFound);
    }
}
