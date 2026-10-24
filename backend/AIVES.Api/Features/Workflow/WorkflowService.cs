using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIVES.Api.Data;
using AIVES.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Workflow;

public class WorkflowService(AivesDbContext db, TimeProvider clock, ScheduleNotifications? notifications = null)
{
    public DateTime Now => clock.GetUtcNow().UtcDateTime;
    public static string Iso(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
    public static ScopeConfig Config(ExamSession exam)
    {
        try { return JsonSerializer.Deserialize<ScopeConfig>(exam.QuestionScopeJson ?? "{}") ?? new(); }
        catch (JsonException) { return new(); }
    }

    public async Task<IDbContextTransaction> LockSchedule(CancellationToken ct)
    {
        var tx = db.Database.IsMySql() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : await db.Database.BeginTransactionAsync(ct);
        // Shared existing role rows serialize schedule mutations across courses and lecturers.
        if (db.Database.IsMySql()) await db.Roles.FromSqlRaw("SELECT * FROM roles ORDER BY role_id FOR UPDATE").ToListAsync(ct);
        return tx;
    }

    public async Task<IDbContextTransaction> LockExam(int id, CancellationToken ct)
    {
        var tx = db.Database.IsMySql() ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : await db.Database.BeginTransactionAsync(ct);
        if (db.Database.IsMySql()) await db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE exam_id={id} FOR UPDATE").ToListAsync(ct);
        return tx;
    }

    public async Task<ExamSession> Owned(int id, int lecturer, CancellationToken ct)
    {
        var exam = await db.ExamSessions.Include(e => e.VivaSettings).Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.ExamId == id, ct);
        Require(exam != null, "Không tìm thấy kỳ thi.", 404);
        Require(exam!.LecturerId == lecturer, "Bạn không quản lý kỳ thi này.", 403);
        return exam;
    }

    public async Task<List<int>> Students(int course, CancellationToken ct, int? classId = null) => await (
        from cs in db.ClassStudents join cl in db.AcademicClasses on cs.ClassId equals cl.ClassId
        join u in db.Users on cs.StudentId equals u.UserId
        where cl.CourseId == course && (!classId.HasValue || cl.ClassId == classId) && cl.IsActive && u.Status == "Active" && u.Role!.RoleName == "Student"
        select cs.StudentId).Distinct().ToListAsync(ct);

    public async Task ValidateSchedule(ExamInput input, int lecturer, int? exclude, CancellationToken ct)
    {
        Require(await db.Courses.AnyAsync(c => c.CourseId == input.CourseId && c.LecturerId == lecturer && c.IsActive, ct), "Bạn không quản lý môn học này.", 403);
        if (input.ClassId.HasValue)
            Require(await db.AcademicClasses.AnyAsync(c => c.ClassId == input.ClassId && c.CourseId == input.CourseId && c.LecturerId == lecturer && c.IsActive, ct), "Lớp không thuộc môn học/quyền quản lý hoặc đã ngừng hoạt động.", 403);
        else
        {
            var legacy = exclude.HasValue ? await db.ExamSessions.AsNoTracking().FirstOrDefaultAsync(e => e.ExamId == exclude && e.LecturerId == lecturer, ct) : null;
            Require(legacy != null && Config(legacy).ClassId == null, "Vui lòng chọn lớp học phần cho kỳ thi.");
        }
        var pool = await db.Questions.CountAsync(q => q.CourseId == input.CourseId && q.Status == "Approved" && q.QuestionType == "Main", ct);
        Validate(input, pool);
        var enrolled = await Students(input.CourseId, ct, input.ClassId);
        Require(input.StudentIds.All(enrolled.Contains), "Sinh viên cần đang hoạt động và đã ghi danh đúng lớp học phần.");
        var start = input.StartTime.UtcDateTime;
        var end = ScheduleEnd(input).UtcDateTime;
        if (input.ClassId.HasValue)
        {
            var peers = await db.ExamSessions.AsNoTracking().Where(e => e.ExamId != exclude && e.CourseId == input.CourseId && e.Status != "Cancelled" && e.Status != "Completed" && e.StartTime < end && e.EndTime > start).ToListAsync(ct);
            Require(!peers.Any(e => Config(e).ClassId == input.ClassId), "Lớp học phần bị trùng lịch thi.", 409);
        }
        Require(!await db.ExamSessions.AnyAsync(e => e.ExamId != exclude && e.Status != "Cancelled" && e.Status != "Completed"
            && e.LecturerId == lecturer && e.StartTime < end && e.EndTime > start, ct), "Giảng viên bị trùng lịch.", 409);
        var slots = await db.ExamCandidates.Where(c => Enumerable.Contains(input.StudentIds, c.StudentId) && c.ExamId != exclude
            && (c.Status == "Waiting" || c.Status == "InProgress") && c.ExamSession!.Status != "Cancelled" && c.ExamSession.Status != "Completed" && c.ScheduledTime != null
            && c.ScheduledTime < end && c.ExamSession.EndTime > start)
            .Select(c => new { c.StudentId, Start = c.ScheduledTime!.Value, Minutes = c.ExamSession!.TimePerStudent }).ToListAsync(ct);
        for (var i = 0; i < input.StudentIds.Length; i++)
        {
            var slot = start.AddMinutes((long)i * input.TimePerStudent);
            Require(!slots.Any(s => s.StudentId == input.StudentIds[i] && Overlaps(slot, slot.AddMinutes(input.TimePerStudent), s.Start, s.Start.AddMinutes(s.Minutes))), "Sinh viên bị trùng slot.", 409);
        }
    }

    public async Task<ExamSession> Save(ExamInput input, int lecturer, int? id, CancellationToken ct)
    {
        await using var tx = await LockSchedule(ct);
        if (id.HasValue && db.Database.IsMySql()) await db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE exam_id={id.Value} FOR UPDATE").ToListAsync(ct);
        var exam = id.HasValue ? await Owned(id.Value, lecturer, ct) : new ExamSession { LecturerId = lecturer, CreatedAt = Now };
        Require(exam.Status is "Draft" or "Scheduled", "Chỉ sửa lịch nháp hoặc lịch đã công bố chưa bắt đầu.", 409);
        Require(!id.HasValue || (exam.StartTime > Now && exam.Candidates.All(c => c.StartedAt == null && c.Status != "InProgress" && c.Status != "Completed")), "Lịch thi đã bắt đầu, không thể thay đổi thời gian hoặc sinh viên.", 409);
        var published = exam.Status == "Scheduled";
        var previousStudents = exam.Candidates.Select(c => c.StudentId).ToArray();
        if (published)
        {
            Require(input.CourseId == exam.CourseId && input.ClassId == Config(exam).ClassId, "Lịch đã công bố không thể đổi môn/lớp.", 409);
            Require(input.MainQuestionCount == exam.MainQuestionCount && input.Strategy == Config(exam).Strategy, "Câu chính và chiến lược đã phân được giữ cố định; có thể sửa thời gian, sinh viên và giới hạn hỏi đào sâu.", 409);
            Require(input.StartTime.UtcDateTime > Now, "Lịch đã công bố cần bắt đầu trong tương lai.");
            Require(!await db.ExamQuestions.AnyAsync(q => q.ExamId == exam.ExamId && (q.AskedAt != null || q.Answer != null), ct), "Kỳ thi đã có lượt trả lời, không thể đổi cấu hình.", 409);
        }
        var end = ScheduleEnd(input);
        await ValidateSchedule(input, lecturer, id, ct);
        if (!published) Require(!await db.ExamQuestions.AnyAsync(q => q.ExamId == exam.ExamId, ct), "Kỳ thi nháp đã có phân câu cần được kiểm tra trước khi sửa.", 409);
        // Preserve previous rows: unselected candidates become Cancelled, never deleted.
        foreach (var c in exam.Candidates) c.Status = "Cancelled";
        for (var i = 0; i < input.StudentIds.Length; i++)
        {
            var student = input.StudentIds[i];
            var c = exam.Candidates.FirstOrDefault(c => c.StudentId == student);
            if (c == null) { c = new ExamCandidate { StudentId = student }; exam.Candidates.Add(c); }
            c.Status = "Waiting";
            c.ScheduledTime = input.StartTime.UtcDateTime.AddMinutes((long)i * input.TimePerStudent);
        }
        exam.CourseId = input.CourseId;
        exam.ExamName = input.ExamName.Trim(); exam.Description = input.Description;
        exam.StartTime = input.StartTime.UtcDateTime; exam.EndTime = end.UtcDateTime;
        exam.TimePerStudent = input.TimePerStudent; exam.MainQuestionCount = input.MainQuestionCount;
        exam.MaxFollowUpCount = input.MaxFollowUpCount;
        JsonObject scope;
        try { scope = JsonNode.Parse(exam.QuestionScopeJson ?? "{}") as JsonObject ?? new(); }
        catch (JsonException) { throw new WorkflowException(409, "Question scope legacy sai JSON; cần kiểm tra trước khi sửa, không tự ghi đè."); }
        scope["Strategy"] = input.Strategy; scope["TimeZone"] = "Asia/Ho_Chi_Minh"; scope["StorageTimeZone"] = "UTC";
        scope["ClassId"] = input.ClassId;
        exam.QuestionScopeJson = scope.ToJsonString();
        exam.VivaSettings ??= new ExamVivaSettings();
        exam.VivaSettings.SecondsPerQuestion = input.SecondsPerQuestion;
        exam.VivaSettings.MaxFollowUpsPerQuestion = input.MaxFollowUpsPerQuestion;
        exam.UpdatedAt = Now;
        if (!id.HasValue) db.ExamSessions.Add(exam);
        await db.SaveChangesAsync(ct);
        if (published) await AssignMissing(exam, exam.Candidates.Where(c => c.Status != "Cancelled").OrderBy(c => c.ScheduledTime).ToList(), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(exam.ExamId, lecturer, published ? previousStudents.Concat(input.StudentIds) : [], published ? "updated" : "draft");
        return exam;
    }

    public async Task<string[]> Publish(int id, int lecturer, CancellationToken ct)
    {
        await using var tx = await LockSchedule(ct);
        var exam = await Owned(id, lecturer, ct);
        if (exam.Status == "Scheduled") { await tx.CommitAsync(ct); return []; }
        Require(exam.Status == "Draft", "Chỉ công bố kỳ thi nháp.", 409);
        Require(!await db.ExamQuestions.AnyAsync(q => q.ExamId == id, ct), "Bản nháp đã có assignment legacy; cần kiểm tra trước khi công bố, không tự phân lại.", 409);
        var candidates = exam.Candidates.Where(c => c.Status != "Cancelled").OrderBy(c => c.ScheduledTime).ToList();
        var config = Config(exam);
        await ValidateSchedule(new(exam.CourseId, exam.ExamName, exam.Description, new(DateTime.SpecifyKind(exam.StartTime, DateTimeKind.Utc)),
            new(DateTime.SpecifyKind(exam.EndTime, DateTimeKind.Utc)), exam.TimePerStudent, exam.MainQuestionCount, exam.MaxFollowUpCount,
            exam.VivaSettings!.MaxFollowUpsPerQuestion, exam.VivaSettings.SecondsPerQuestion, config.Strategy, candidates.Select(c => c.StudentId).ToArray(), config.ClassId), lecturer, id, ct);
        Require(exam.EndTime > Now, "Không công bố kỳ thi đã hết giờ.");
        var warnings = await AssignMissing(exam, candidates, ct);
        exam.Status = "Scheduled"; exam.UpdatedAt = Now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        if (notifications != null) await notifications.Changed(id, lecturer, candidates.Select(c => c.StudentId), "published");
        return warnings;
    }

    private async Task<string[]> AssignMissing(ExamSession exam, List<ExamCandidate> candidates, CancellationToken ct)
    {
        var config = Config(exam);
        var pool = await db.Questions.Where(q => q.CourseId == exam.CourseId && q.Status == "Approved" && q.QuestionType == "Main").ToListAsync(ct);
        var past = await db.ExamQuestions.Where(q => q.ExamSession!.CourseId == exam.CourseId && q.ParentExamQuestionId == null)
            .OrderByDescending(q => q.ExamQuestionId).Take(1000).Select(q => new { q.QuestionId, q.CandidateId }).ToListAsync(ct);
        var usage = past.GroupBy(q => q.QuestionId).ToDictionary(g => g.Key, g => g.Count());
        var recent = new Queue<List<int>>(past.Select(p => p.CandidateId).Distinct().Take(2).Select(c => past.Where(p => p.CandidateId == c).Select(p => p.QuestionId).ToList()));
        foreach (var c in candidates)
        {
            var existing = await db.ExamQuestions.CountAsync(q => q.CandidateId == c.CandidateId && q.ExamId == exam.ExamId && q.ParentExamQuestionId == null, ct);
            if (existing > 0) { Require(existing == exam.MainQuestionCount, "Phân câu hiện tại không khớp cấu hình; cần kiểm tra trước khi sửa lịch.", 409); continue; }
            var selected = Allocate(pool, exam.MainQuestionCount, recent.SelectMany(x => x), usage, config.Strategy);
            for (var i = 0; i < selected.Count; i++)
            {
                db.ExamQuestions.Add(new() { ExamId = exam.ExamId, CandidateId = c.CandidateId, QuestionId = selected[i].QuestionId, OrderNo = i + 1 });
                usage[selected[i].QuestionId] = usage.GetValueOrDefault(selected[i].QuestionId) + 1;
            }
            recent.Enqueue(selected.Select(q => q.QuestionId).ToList()); if (recent.Count > 2) recent.Dequeue();
        }
        return pool.Count < exam.MainQuestionCount * 3 ? ["Pool nhỏ: có thể trùng câu trong ba thí sinh liên tiếp; đã ưu tiên câu chưa dùng gần đây."] : [];
    }

    public async Task Notify(int examId, string reason, CancellationToken ct)
    {
        if (notifications == null) return;
        var exam = await db.ExamSessions.AsNoTracking().Include(e => e.Candidates).FirstAsync(e => e.ExamId == examId, ct);
        await notifications.Changed(examId, exam.LecturerId, exam.Candidates.Select(c => c.StudentId), reason);
    }

    public void Log(int exam, int? candidate, string action, string? description = null) => db.ExamLogs.Add(new()
        { ExamId = exam, CandidateId = candidate, Action = action, Description = description, Timestamp = Now });

    public async Task<string> CancelAsync(int id, int lecturer, CancellationToken ct)
    {
        await using var tx = await LockSchedule(ct);
        // Lock exam too, following schedule->exam lock ordering used by publication.
        if (db.Database.IsMySql()) await db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE exam_id={id} FOR UPDATE").ToListAsync(ct);
        var exam = await Owned(id, lecturer, ct);
        WorkflowRules.Require(exam.Status is "Draft" or "Scheduled", "Chỉ hủy nháp hoặc lịch chưa bắt đầu.", 409);
        exam.Status = "Cancelled";
        foreach (var c in exam.Candidates) c.Status = "Cancelled";
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Notify(id, "cancelled", ct); return exam.Status;
    }
    public async Task<object> ScheduleAsync(int student, CancellationToken ct)
    {
        var rows = await db.ExamCandidates.Where(c => c.StudentId == student && c.Status != "Cancelled" && c.ExamSession!.Status != "Draft" && c.ExamSession.Status != "Cancelled")
            .OrderBy(c => c.ScheduledTime).Select(c => new { c.ExamId, c.CandidateId, c.Status, c.ScheduledTime,
                c.ExamSession!.ExamName, c.ExamSession.TimePerStudent, CourseName = c.ExamSession.Course!.CourseName }).ToListAsync(ct);
        return rows.Select(c => new { c.ExamId, c.CandidateId, c.Status, c.ExamName, c.TimePerStudent, c.CourseName,
            scheduledTime = c.ScheduledTime.HasValue ? WorkflowService.Iso(c.ScheduledTime.Value) : null }).ToArray();
    }
    public async Task<object> CatalogAsync(int lecturer, CancellationToken ct) => await db.Courses
        .Where(c => c.LecturerId == lecturer && c.IsActive)
        .Select(c => new { c.CourseId, c.CourseCode, c.CourseName, poolSize = db.Questions.Count(q => q.CourseId == c.CourseId && q.Status == "Approved" && q.QuestionType == "Main") }).ToListAsync(ct);

    public async Task<object> CourseStudentsAsync(int id, int lecturer, CancellationToken ct)
    {
        Require(await db.Courses.AnyAsync(c => c.CourseId == id && c.LecturerId == lecturer, ct), "Bạn không quản lý môn học này.", 403);
        var ids = await Students(id, ct);
        return await db.Users.Where(u => ids.Contains(u.UserId)).OrderBy(u => u.FullName)
            .Select(u => new { u.UserId, u.FullName, u.Email }).ToListAsync(ct);
    }

}
