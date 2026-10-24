using System.Text.RegularExpressions;
using AIVES.Api.Data;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Lecturer;

public record CourseInput(string CourseCode, string CourseName, string? Description);
public record ClassInput(int CourseId, string ClassCode, string ClassName);
public record QuestionInput(int CourseId, string QuestionText, string? ExpectedAnswer, string Difficulty, string BloomLevel, string Status);
public record CsvResult(int Added, int AlreadyEnrolled, string[] Errors);

public class LecturerService(AivesDbContext db, WorkflowService flow, ScheduleNotifications? notifications = null)
{
    public async Task<Course> Course(int id, int lecturer, CancellationToken ct)
    {
        var course = await db.Courses.FindAsync([id], ct);
        Require(course != null, "Không tìm thấy môn học.", 404);
        Require(course.LecturerId == lecturer, "Bạn không phụ trách môn học này.", 403);
        return course;
    }
    public async Task<AcademicClass> Class(int id, int lecturer, CancellationToken ct)
    {
        var item = await db.AcademicClasses.Include(c => c.Course).FirstOrDefaultAsync(c => c.ClassId == id, ct);
        Require(item != null, "Không tìm thấy lớp học phần.", 404);
        Require(item.LecturerId == lecturer && item.Course!.LecturerId == lecturer, "Bạn không phụ trách lớp/môn học này.", 403);
        return item;
    }
    private static string Code(string value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        Require(Regex.IsMatch(code, "^[A-Z0-9][A-Z0-9._-]{0,49}$"), "Mã gồm 1–50 ký tự chữ, số, dấu chấm/gạch ngang/gạch dưới."); return code;
    }
    private static string Name(string value)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200, "Tên cần từ 1–200 ký tự."); return value.Trim();
    }
    public async Task<int> SaveCourse(int? id, int lecturer, CourseInput input, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        var code = Code(input.CourseCode); var name = Name(input.CourseName);
        Require(input.Description?.Length is not > 1000, "Mô tả tối đa 1000 ký tự.");
        Require(!await db.Courses.AnyAsync(c => c.CourseCode.ToUpper() == code && c.CourseId != id, ct), "Mã môn học đã tồn tại.", 409);
        var item = id.HasValue ? await Course(id.Value, lecturer, ct) : new Course { LecturerId = lecturer, CreatedAt = flow.Now };
        item.CourseCode = code; item.CourseName = name; item.Description = input.Description?.Trim(); item.UpdatedAt = flow.Now;
        if (!id.HasValue) db.Courses.Add(item);
        await Save(ct); await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(0, lecturer, await db.ClassStudents.Where(s => s.AcademicClass!.CourseId == item.CourseId).Select(s => s.StudentId).Distinct().ToListAsync(ct), "course");
        return item.CourseId;
    }
    public async Task<int> SaveClass(int? id, int lecturer, ClassInput input, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        var course = await Course(input.CourseId, lecturer, ct); Require(course.IsActive, "Môn học đã ngừng hoạt động.");
        var code = Code(input.ClassCode); var name = Name(input.ClassName);
        Require(!await db.AcademicClasses.AnyAsync(c => c.ClassCode.ToUpper() == code && c.ClassId != id, ct), "Mã lớp học phần đã tồn tại.", 409);
        var item = id.HasValue ? await Class(id.Value, lecturer, ct) : new AcademicClass { LecturerId = lecturer, CourseId = input.CourseId, CreatedAt = flow.Now };
        Require(item.CourseId == input.CourseId, "Không đổi môn của lớp đã tạo; hãy tạo lớp học phần khác.", 409);
        item.ClassCode = code; item.ClassName = name; item.UpdatedAt = flow.Now;
        if (!id.HasValue) db.AcademicClasses.Add(item);
        await Save(ct); await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(0, lecturer, await db.ClassStudents.Where(s => s.ClassId == item.ClassId).Select(s => s.StudentId).ToListAsync(ct), "class");
        return item.ClassId;
    }
    public async Task Enroll(int id, int lecturer, int student, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        var cl = await Class(id, lecturer, ct); Require(cl.IsActive && cl.Course!.IsActive, "Lớp/môn đã ngừng hoạt động.");
        Require(await db.Users.AnyAsync(u => u.UserId == student && u.Status == "Active" && u.Role!.RoleName == "Student", ct), "Chỉ ghi danh tài khoản Sinh viên đang hoạt động.");
        if (!await db.ClassStudents.AnyAsync(c => c.ClassId == id && c.StudentId == student, ct))
        { db.ClassStudents.Add(new() { ClassId = id, StudentId = student, JoinedAt = flow.Now }); await Save(ct); }
        await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(0, lecturer, [student], "enrollment");
    }
    public async Task Unenroll(int id, int lecturer, int student, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        var cl = await Class(id, lecturer, ct);
        var exams = await db.ExamCandidates.Where(c => c.StudentId == student && c.Status != "Cancelled" && c.ExamSession!.CourseId == cl.CourseId
            && c.ExamSession.Status != "Cancelled" && c.ExamSession.Status != "Completed").Select(c => c.ExamSession!).ToListAsync(ct);
        Require(!exams.Any(e => WorkflowService.Config(e).ClassId == id || WorkflowService.Config(e).ClassId == null), "Sinh viên đang có lịch thi/nháp liên quan. Hủy hoặc sửa lịch trước khi gỡ ghi danh.", 409);
        var enrollment = await db.ClassStudents.FindAsync([id, student], ct);
        if (enrollment != null) { db.ClassStudents.Remove(enrollment); await Save(ct); }
        await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(0, lecturer, [student], "enrollment");
    }
    public async Task<CsvResult> Import(int id, int lecturer, string csv, CancellationToken ct)
    {
        Require(csv.Length <= 100000, "CSV tối đa 100KB.");
        var lines = csv.TrimStart('\uFEFF').TrimEnd('\r', '\n').Split('\n');
        Require(lines.Length <= 501 && lines[0].Trim().Equals("email", StringComparison.OrdinalIgnoreCase), "CSV cần cột duy nhất email, tối đa 500 sinh viên.");
        await using var tx = await flow.LockSchedule(ct);
        var cl = await Class(id, lecturer, ct); Require(cl.IsActive && cl.Course!.IsActive, "Lớp/môn đã ngừng hoạt động.");
        var errors = new List<string>(); var ids = new HashSet<int>();
        for (var i = 1; i < lines.Length; i++)
        {
            var email = lines[i].Trim().Trim('"').ToLowerInvariant(); if (email == "") continue;
            var student = await db.Users.Where(u => u.Email.ToLower() == email && u.Status == "Active" && u.Role!.RoleName == "Student").Select(u => (int?)u.UserId).FirstOrDefaultAsync(ct);
            if (student == null) errors.Add($"Dòng {i + 1}: không có tài khoản Sinh viên hoạt động với email này.");
            else if (!ids.Add(student.Value)) errors.Add($"Dòng {i + 1}: email bị lặp trong CSV.");
        }
        if (errors.Count > 0) { await tx.CommitAsync(ct); return new(0, 0, errors.ToArray()); }
        var existing = await db.ClassStudents.Where(c => c.ClassId == id).Select(c => c.StudentId).ToListAsync(ct);
        foreach (var student in ids.Except(existing)) db.ClassStudents.Add(new() { ClassId = id, StudentId = student, JoinedAt = flow.Now });
        await Save(ct); await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(0, lecturer, ids, "enrollment");
        return new(ids.Except(existing).Count(), ids.Intersect(existing).Count(), []);
    }
    public async Task<int> SaveQuestion(int? id, int lecturer, QuestionInput input, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        await Course(input.CourseId, lecturer, ct);
        Require(!string.IsNullOrWhiteSpace(input.QuestionText) && input.QuestionText.Length <= 5000, "Câu hỏi cần từ 1–5000 ký tự.");
        Require(input.ExpectedAnswer?.Length is not > 20000, "Đáp án tham khảo tối đa 20000 ký tự.");
        Require(input.Difficulty is "Easy" or "Medium" or "Hard", "Độ khó không hợp lệ.");
        Require(input.BloomLevel is "Remember" or "Understand" or "Apply" or "Analyze" or "Evaluate" or "Create", "Mức độ nhận thức không hợp lệ.");
        Require(input.Status is "Draft" or "Approved", "Trạng thái cần là Nháp hoặc Sẵn sàng.");
        var item = id.HasValue ? await db.Questions.Include(q => q.Course).FirstOrDefaultAsync(q => q.QuestionId == id, ct) : null;
        if (id.HasValue)
        {
            Require(item != null, "Không tìm thấy câu hỏi.", 404);
            Require(item.Course!.LecturerId == lecturer && item.CourseId == input.CourseId, "Không đổi môn hoặc sửa câu hỏi của người khác.", 403);
            Require(!await db.ExamQuestions.AnyAsync(q => q.QuestionId == id, ct), "Câu đã phân vào đề thi; hãy tạo câu mới để bảo toàn lịch sử.", 409);
            Require(item.Source == "Manual" && item.QuestionType == "Main", "Chỉ sửa câu chính nhập thủ công tại màn hình này.", 409);
        }
        else { item = new() { CourseId = input.CourseId, CreatedBy = lecturer, CreatedAt = flow.Now, Source = "Manual", QuestionType = "Main" }; db.Questions.Add(item); }
        item!.QuestionText = input.QuestionText.Trim(); item.ExpectedAnswer = input.ExpectedAnswer?.Trim(); item.Difficulty = input.Difficulty;
        item.BloomLevel = input.BloomLevel; item.Status = input.Status; item.UpdatedAt = flow.Now;
        if (input.Status == "Approved") { item.ReviewedBy = lecturer; item.ReviewedAt = flow.Now; }
        await Save(ct); await tx.CommitAsync(ct); return item.QuestionId;
    }
    public async Task ArchiveQuestion(int id, int lecturer, CancellationToken ct)
    {
        await using var tx = await flow.LockSchedule(ct);
        var q = await db.Questions.Include(q => q.Course).FirstOrDefaultAsync(q => q.QuestionId == id, ct);
        Require(q != null, "Không tìm thấy câu hỏi.", 404); Require(q.Course!.LecturerId == lecturer, "Không có quyền quản lý câu hỏi.", 403);
        q.Status = "Archived"; q.UpdatedAt = flow.Now; await Save(ct); await tx.CommitAsync(ct);
    }
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new WorkflowException(409, "Dữ liệu bị trùng hoặc ràng buộc đã thay đổi. Vui lòng tải lại."); }
    }

    public async Task<object> DashboardAsync(int lecturer, CancellationToken ct)
    {
        var courses = db.Courses.Where(c => c.LecturerId == lecturer && c.IsActive);
        var classes = db.AcademicClasses.Where(c => c.LecturerId == lecturer && c.Course!.LecturerId == lecturer && c.IsActive);
        return new { courseCount = await courses.CountAsync(ct), classCount = await classes.CountAsync(ct),
            studentCount = await db.ClassStudents.Where(s => classes.Select(c => c.ClassId).Contains(s.ClassId)).Select(s => s.StudentId).Distinct().CountAsync(ct),
            questionCount = await db.Questions.CountAsync(q => q.Course!.LecturerId == lecturer && q.Status != "Archived", ct),
            documentCount = await db.Documents.CountAsync(d => d.Course!.LecturerId == lecturer && d.Status != "Archived", ct),
            upcomingCount = await db.ExamSessions.CountAsync(e => e.LecturerId == lecturer && (e.Status == "Scheduled" || e.Status == "InProgress") && e.EndTime > flow.Now, ct),
            upcoming = await db.ExamSessions.Where(e => e.LecturerId == lecturer && (e.Status == "Scheduled" || e.Status == "InProgress") && e.EndTime > flow.Now).OrderBy(e => e.StartTime).Take(5)
                .Select(e => new { e.ExamId, e.ExamName, e.StartTime, e.EndTime, e.Status, courseName = e.Course!.CourseName }).ToListAsync(ct) };
    }
    public async Task<object> SearchStudentsAsync(string? q, int page, CancellationToken ct)
    {
        var term = q?.Trim() ?? "";
        WorkflowRules.Require(term is { Length: >= 2 and <= 100 } && page is > 0 and <= 1000, "Nhập ít nhất 2 ký tự tên/email; trang cần lớn hơn 0.");
        return await db.Users.Where(u => u.Role!.RoleName == "Student" && u.Status == "Active" && (u.Email.Contains(term) || u.FullName.Contains(term)))
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Skip((page - 1) * 30).Take(30).Select(u => new { u.UserId, u.FullName, u.Email }).ToListAsync(ct);
    }
    public async Task<object> ClassStudentsAsync(int id, int lecturer, CancellationToken ct)
    {
        await Class(id, lecturer, ct);
        return await db.ClassStudents.Where(s => s.ClassId == id).OrderBy(s => s.Student!.FullName)
            .Select(s => new { s.StudentId, userId = s.StudentId, s.Student!.FullName, s.Student.Email, s.Student.Status, s.JoinedAt }).ToListAsync(ct);
    }

}
