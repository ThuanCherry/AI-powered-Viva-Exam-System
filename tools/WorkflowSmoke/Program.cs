using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AIVES.Api.Data;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Adds a NEW tagged fixture only. Never deletes, resets, imports or changes existing records.
// Intended for a local development DB; credentials are saved only in ignored artifacts/.
var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("backend/AIVES.Api"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.local.json", true)
    .AddUserSecrets("AIVES-Workflow-Local", true).AddEnvironmentVariables().Build();
var options = new DbContextOptionsBuilder<AivesDbContext>().UseMySql(config.GetConnectionString("DefaultConnection"), new MySqlServerVersion(new Version(8, 0, 0))).Options;
await using var db = new AivesDbContext(options);
var tag = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + RandomNumberGenerator.GetHexString(4).ToLowerInvariant();
var password = Environment.GetEnvironmentVariable("AIVES_DEMO_PASSWORD") ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
await using (var tx = await db.Database.BeginTransactionAsync())
{
    var lecturerRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Lecturer");
    var studentRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Student");
    if (lecturerRole == null) { lecturerRole = new Role { RoleName = "Lecturer" }; db.Roles.Add(lecturerRole); }
    if (studentRole == null) { studentRole = new Role { RoleName = "Student" }; db.Roles.Add(studentRole); }
    await db.SaveChangesAsync();
    var lecturer = new User { RoleId = lecturerRole.RoleId, FullName = "Giảng viên Demo", Email = $"lecturer.{tag}@demo.invalid", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), CreatedAt = DateTime.UtcNow };
    var outsider = new User { RoleId = lecturerRole.RoleId, FullName = "Giảng viên khác Demo", Email = $"other.{tag}@demo.invalid", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), CreatedAt = DateTime.UtcNow };
    var students = Enumerable.Range(1, 2).Select(i => new User { RoleId = studentRole.RoleId, FullName = $"Sinh viên Demo {i}", Email = $"student{i}.{tag}@demo.invalid", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), CreatedAt = DateTime.UtcNow }).ToArray();
    db.Users.AddRange([lecturer, outsider, ..students]); await db.SaveChangesAsync();
    var course = new Course { CourseCode = $"DEMO-{tag}", CourseName = "Thiết kế phần mềm · Demo", LecturerId = lecturer.UserId, CreatedAt = DateTime.UtcNow }; db.Courses.Add(course); await db.SaveChangesAsync();
    var cl = new AcademicClass { CourseId = course.CourseId, LecturerId = lecturer.UserId, ClassCode = $"DEMO-{tag}", ClassName = "Lớp kiểm thử workflow", CreatedAt = DateTime.UtcNow }; db.AcademicClasses.Add(cl); await db.SaveChangesAsync();
    db.ClassStudents.AddRange(students.Select(s => new ClassStudent { ClassId = cl.ClassId, StudentId = s.UserId, JoinedAt = DateTime.UtcNow }));
    var texts = new[] { "Đóng gói trong lập trình hướng đối tượng có lợi ích gì?", "Giải thích khác biệt giữa interface và abstract class.", "Khi nào nên dùng dependency injection?", "Transaction giúp bảo vệ dữ liệu như thế nào?", "Vì sao cần kiểm tra quyền ở backend?", "Giải thích một ví dụ áp dụng nguyên tắc Single Responsibility.", "REST API cần xử lý request lặp như thế nào?", "Phân biệt authentication và authorization.", "Thiết kế monolithic phù hợp trong trường hợp nào?" };
    db.Questions.AddRange(texts.Select((text, i) => new Question { CourseId = course.CourseId, CreatedBy = lecturer.UserId, QuestionText = text, ExpectedAnswer = "Giải thích khái niệm chính xác, liên hệ ví dụ và nêu đánh đổi phù hợp.", Status = "Approved", BloomLevel = "Understand", Difficulty = new[] { "Easy", "Medium", "Hard" }[i % 3], CreatedAt = DateTime.UtcNow }));
    await db.SaveChangesAsync(); await tx.CommitAsync();
    Directory.CreateDirectory("artifacts");
    await File.WriteAllTextAsync("artifacts/demo-credentials.local.json", JsonSerializer.Serialize(new { lecturer = lecturer.Email, otherLecturer = outsider.Email, students = students.Select(s => s.Email), password, courseId = course.CourseId, classId = cl.ClassId, studentIds = students.Select(s => s.UserId) }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Created new demo fixture. Private login information: artifacts/demo-credentials.local.json (gitignored).");
    if (args.Contains("--seed-only")) return;

    var baseUrl = Environment.GetEnvironmentVariable("AIVES_BASE_URL") ?? "http://localhost:5234";
    using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(70) };
    var passed = 0;
    async Task<JsonElement> Call(HttpMethod method, string path, object? body = null, string? token = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var req = new HttpRequestMessage(method, path);
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null) req.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(req);
        if (res.StatusCode != expected) throw new Exception($"FAIL {method} {path}: expected {(int)expected}, received {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        passed++; if (res.Content.Headers.ContentLength == 0 || !res.Content.Headers.ContentType?.MediaType?.Contains("json") == true) return default;
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(); return json.TryGetProperty("data", out var data) ? data : json;
    }
    async Task<string> Login(string email) => (await Call(HttpMethod.Post, "/api/Users/login", new { email, password })).GetProperty("token").GetString()!;
    var lt = await Login(lecturer.Email); var st = await Login(students[0].Email); var other = await Login(outsider.Email); var st2 = await Login(students[1].Email);
    await Call(HttpMethod.Get, "/health");
    await Call(HttpMethod.Get, "/api/Exams", expected: HttpStatusCode.Unauthorized);
    await Call(HttpMethod.Get, "/api/Questions", token: st, expected: HttpStatusCode.Forbidden);
    await Call(HttpMethod.Post, "/api/Exams", new { }, st, HttpStatusCode.Forbidden);
    var start = DateTimeOffset.UtcNow.AddMinutes(-1);
    object Input(DateTimeOffset when, int minutes = 15) => new { courseId = course.CourseId, classId = cl.ClassId, examName = $"Workflow smoke {tag}", description = "New demo records only", startTime = when, endTime = when.AddHours(1), timePerStudent = minutes,
        mainQuestionCount = 2, maxFollowUpCount = 2, maxFollowUpsPerQuestion = 1, secondsPerQuestion = 120, strategy = "Random", studentIds = new[] { students[0].UserId } };
    await Call(HttpMethod.Post, "/api/Exams", Input(start, 0), lt, HttpStatusCode.BadRequest);
    var created = await Call(HttpMethod.Post, "/api/Exams", Input(start), lt); var id = created.GetProperty("examId").GetInt32();
    await Call(HttpMethod.Get, $"/api/Exams/{id}", token: other, expected: HttpStatusCode.Forbidden);
    await Call(HttpMethod.Get, $"/api/Exams/{id}", token: st, expected: HttpStatusCode.Forbidden);
    await Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/start", token: st, expected: HttpStatusCode.Conflict);
    await Call(HttpMethod.Put, $"/api/Exams/{id}", Input(start), lt);
    await Call(HttpMethod.Post, "/api/Exams", Input(start), lt, HttpStatusCode.Conflict);
    await Call(HttpMethod.Post, $"/api/Exams/{id}/publish", token: lt);
    await Call(HttpMethod.Post, $"/api/Exams/{id}/publish", token: lt);
    await Call(HttpMethod.Put, $"/api/Exams/{id}", Input(start), lt, HttpStatusCode.Conflict);
    await Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/start", token: st2, expected: HttpStatusCode.Forbidden);
    await Call(HttpMethod.Get, "/api/schedule/mine", token: st);
    var concurrent = await Task.WhenAll(Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/start", token: st), Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/start", token: st));
    var turn = concurrent[0].GetProperty("turn").GetProperty("turnId").GetString();
    if (turn != concurrent[1].GetProperty("turn").GetProperty("turnId").GetString()) throw new Exception("Concurrent start changed turn");
    var state = await Call(HttpMethod.Get, $"/api/Exams/{id}/attempt", token: st);
    if (state.ToString().Contains("expectedAnswer", StringComparison.OrdinalIgnoreCase)) throw new Exception("Student response leaks expected answer");
    var status = await Call(HttpMethod.Get, "/api/workflow/providers", token: lt);
    Console.WriteLine($"Providers: {status}. Configured is not a claim of successful live verification.");
    if (status.GetProperty("tts").GetString() == "NotConfigured")
        await Call(HttpMethod.Get, $"/api/Exams/{id}/attempt/tts?turnId={turn}", token: st, expected: HttpStatusCode.ServiceUnavailable);
    // A canonical 100ms silent PCM chunk exercises multipart validation and the NotConfigured adapter path.
    if (status.GetProperty("stt").GetString() == "NotConfigured")
    {
        using var wavStream = new MemoryStream();
        using (var writer = new BinaryWriter(wavStream, System.Text.Encoding.ASCII, true))
        {
            writer.Write("RIFF"u8); writer.Write(3236); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(3200); writer.Write(new byte[3200]);
        }
        using var upload = new HttpRequestMessage(HttpMethod.Post, $"/api/Exams/{id}/attempt/transcribe");
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", st);
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(turn!), "turnId"); form.Add(new StringContent(Guid.NewGuid().ToString()), "chunkId");
        using var audioContent = new ByteArrayContent(wavStream.ToArray()); audioContent.Headers.ContentType = new("audio/wav"); form.Add(audioContent, "audio", "chunk.wav"); upload.Content = form;
        using var uploadResponse = await http.SendAsync(upload);
        if (uploadResponse.StatusCode != HttpStatusCode.ServiceUnavailable) throw new Exception($"STT expected NotConfigured 503, received {(int)uploadResponse.StatusCode}");
        passed++;
    }
    await Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/answer", new { turnId = "q-999999", transcript = "bad" }, st, HttpStatusCode.Conflict);
    var response = await Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/answer", new { turnId = turn, transcript = "Đây là câu trả lời thử nghiệm, em chưa nắm rõ khái niệm." }, st);
    var calls = await Task.WhenAll(Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/answer", new { turnId = turn, transcript = "retry" }, st), Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/answer", new { turnId = turn, transcript = "retry" }, st));
    state = calls[0];
    for (var i = 0; i < 6 && state.GetProperty("status").GetString() == "InProgress"; i++)
        state = await Call(HttpMethod.Post, $"/api/Exams/{id}/attempt/answer", new { turnId = state.GetProperty("turn").GetProperty("turnId").GetString(), transcript = "Em hiểu đây là cách tổ chức hệ thống nhưng cần giải thích thêm." }, st);
    if (state.GetProperty("status").GetString() != "Completed") throw new Exception("Attempt did not complete within configured limits");
    db.ChangeTracker.Clear();
    if (await db.ExamQuestions.CountAsync(q => q.ExamId == id) != 2) throw new Exception("Duplicate assignments");
    if (await db.Answers.CountAsync(a => a.ExamQuestion!.ExamId == id) != 2) throw new Exception("Duplicate/missing answers");
    await Call(HttpMethod.Get, $"/api/Exams/{id}/attempt", token: st);
    // Leave a future published exam for visual/manual demo, with both students.
    var future = await Call(HttpMethod.Post, "/api/Exams", Input(DateTimeOffset.UtcNow.AddHours(2)), lt);
    var futureId = future.GetProperty("examId").GetInt32();
    await Call(HttpMethod.Post, $"/api/Exams/{futureId}/publish", token: lt);
    await Call(HttpMethod.Post, $"/api/Exams/{futureId}/attempt/start", token: st, expected: HttpStatusCode.Conflict);
    await Call(HttpMethod.Post, $"/api/Exams/{futureId}/cancel", token: lt);
    var draft = await Call(HttpMethod.Post, "/api/Exams", Input(DateTimeOffset.UtcNow.AddHours(3)), lt);
    Console.WriteLine($"PASS: {passed} HTTP checks against real MySQL, including concurrent start/retry and persisted completion. Demo draft exam: {draft.GetProperty("examId")}. No existing data overwritten or deleted.");
    if (status.GetProperty("llm").GetString() != "Configured") Console.WriteLine("SKIP live Gemini: no configured key/model. Manual text fallback and persisted NotConfigured were exercised.");
    if (status.GetProperty("stt").GetString() != "Configured") Console.WriteLine("SKIP live Gemini STT/TTS: no configured Gemini key/provider. Microphone/provider voice quality remains unverified.");
}
