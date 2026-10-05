using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.EntityFrameworkCore;
namespace AIVES.BusinessLogicLayer.Services;
public class CourseService(IUnitOfWork uow, IUserService users) : ICourseService
{
    public async Task<IReadOnlyList<Course>> GetForLecturerAsync(long lecturerId, bool includeInactive = false)
    {
        var assigned = (await uow.CourseLecturers.FindAsync(x => x.LecturerId == lecturerId)).Select(x => x.CourseId).ToArray();
        return (await uow.Courses.FindAsync(x => assigned.Contains(x.CourseId) && (includeInactive || x.IsActive))).OrderBy(x => x.CourseCode).ToArray();
    }
    private async Task<Course?> Owned(long id, long owner)
    {
        var identity = await users.GetIdentityAsync(owner);
        if (identity == null || !identity.Roles.Contains("LECTURER") ||
            !await uow.CourseLecturers.ExistsAsync(x => x.CourseId == id && x.LecturerId == owner)) return null;
        return await uow.Courses.GetByIdAsync(id);
    }
    public async Task<ServiceResult<CourseDetails>> GetDetailsAsync(long courseId, long lecturerId)
    {
        var course = await Owned(courseId, lecturerId);
        if (course == null) return ServiceResult<CourseDetails>.FailureResult("Bạn không phụ trách môn học này.");
        var questions = (await uow.Questions.FindAsync(x => x.CourseId == courseId)).OrderBy(x => x.QuestionId).ToArray();
        var role = (await uow.Roles.FindAsync(x => x.Code == "STUDENT")).SingleOrDefault();
        var studentIds = role == null ? [] : (await uow.UserRoles.FindAsync(x => x.RoleId == role.RoleId)).Select(x => x.UserId).ToArray();
        var students = (await uow.Users.FindAsync(x => studentIds.Contains(x.UserId) && x.Status == "ACTIVE")).OrderBy(x => x.Email).ToArray();
        var enrolled = (await uow.CourseStudents.FindAsync(x => x.CourseId == courseId && x.IsActive)).Select(x => x.StudentId).ToArray();
        var examIds = (await uow.Exams.FindAsync(x => x.CourseId == courseId && x.Status != "DRAFT")).Select(x => x.ExamId).ToArray();
        var locked = (await uow.QuestionPool.FindAsync(x => examIds.Contains(x.ExamId))).Select(x => x.QuestionId).ToHashSet();
        return ServiceResult<CourseDetails>.SuccessResult(new(course, questions, students, enrolled, locked));
    }
    private static string? Validate(CourseCommand c)
    {
        if (string.IsNullOrWhiteSpace(c.Code) || c.Code.Trim().Length > 50) return "Mã môn học cần từ 1 đến 50 ký tự.";
        if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 255) return "Tên môn học cần từ 1 đến 255 ký tự.";
        if (c.Description != null && System.Text.Encoding.UTF8.GetByteCount(c.Description) > 60000) return "Mô tả môn học quá dài; hãy rút gọn nội dung.";
        return null;
    }
    public async Task<ServiceResult<Course>> CreateAsync(CourseCommand c, long lecturerId)
    {
        var identity = await users.GetIdentityAsync(lecturerId);
        if (identity == null || !identity.Roles.Contains("LECTURER")) return ServiceResult<Course>.FailureResult("Chỉ giảng viên được tạo môn học.");
        var error = Validate(c);
        if (error != null) return ServiceResult<Course>.FailureResult(error);
        var code = c.Code.Trim().ToUpperInvariant();
        if (await uow.Courses.ExistsAsync(x => x.CourseCode == code)) return ServiceResult<Course>.FailureResult("Mã môn học đã tồn tại.");
        try
        {
            return await uow.InTransactionAsync(null, async () =>
            {
                var course = new Course { CourseCode = code, CourseName = c.Name.Trim(), Description = c.Description, IsActive = c.IsActive };
                await uow.Courses.AddAsync(course);
                await uow.SaveChangesAsync();
                await uow.CourseLecturers.AddAsync(new CourseLecturer { CourseId = course.CourseId, LecturerId = lecturerId });
                await uow.SaveChangesAsync();
                return ServiceResult<Course>.SuccessResult(course, "Đã tạo môn học và phân công bạn phụ trách.");
            }, x => x.Success);
        }
        catch (DbUpdateException) { return ServiceResult<Course>.FailureResult("Không thể lưu. Mã môn học có thể đã tồn tại."); }
    }
    public async Task<ServiceResult<Course>> UpdateAsync(long courseId, CourseCommand c, long lecturerId)
    {
        try
        {
            return await uow.InTransactionAsync(null, async () =>
            {
                var course = await Owned(courseId, lecturerId);
                if (course == null) return ServiceResult<Course>.FailureResult("Bạn không phụ trách môn học này.");
                var error = Validate(c);
                if (error != null) return ServiceResult<Course>.FailureResult(error);
                var code = c.Code.Trim().ToUpperInvariant();
                if (await uow.Courses.ExistsAsync(x => x.CourseId != courseId && x.CourseCode == code))
                    return ServiceResult<Course>.FailureResult("Mã môn học đã tồn tại.");
                course.CourseCode = code; course.CourseName = c.Name.Trim(); course.Description = c.Description;
                course.IsActive = c.IsActive; course.UpdatedAt = DateTime.UtcNow;
                await uow.SaveChangesAsync();
                return ServiceResult<Course>.SuccessResult(course, "Đã cập nhật môn học.");
            }, x => x.Success, courseId);
        }
        catch (DbUpdateException) { return ServiceResult<Course>.FailureResult("Không thể lưu. Mã môn học có thể đã tồn tại."); }
    }
    public Task<ServiceResult<bool>> SetStudentsAsync(long courseId, IReadOnlyList<long> studentIds, long lecturerId) =>
        uow.InTransactionAsync(null, async () =>
        {
            var detail = await GetDetailsAsync(courseId, lecturerId);
            if (!detail.Success) return ServiceResult<bool>.FailureResult(detail.Message);
            var eligible = detail.Data!.Students.Select(x => x.UserId).ToHashSet();
            if (studentIds.Distinct().Count() != studentIds.Count || studentIds.Any(x => !eligible.Contains(x)))
                return ServiceResult<bool>.FailureResult("Chọn sinh viên đang hoạt động; danh sách không được trùng.");
            var existing = (await uow.CourseStudents.FindAsync(x => x.CourseId == courseId)).ToArray();
            foreach (var enrollment in existing) enrollment.IsActive = studentIds.Contains(enrollment.StudentId);
            foreach (var id in studentIds.Where(x => !existing.Any(e => e.StudentId == x)))
                await uow.CourseStudents.AddAsync(new CourseStudent { CourseId = courseId, StudentId = id });
            await uow.SaveChangesAsync();
            return ServiceResult<bool>.SuccessResult(true, "Đã cập nhật sinh viên của môn học.");
        }, x => x.Success, courseId);

    public Task<ServiceResult<Question>> SaveQuestionAsync(long courseId, long? questionId, QuestionCommand c, long lecturerId) =>
        uow.InTransactionAsync(null, async () =>
        {
            var detail = await GetDetailsAsync(courseId, lecturerId);
            if (!detail.Success) return ServiceResult<Question>.FailureResult(detail.Message);
            if (string.IsNullOrWhiteSpace(c.Content) || System.Text.Encoding.UTF8.GetByteCount(c.Content) > 60000 ||
                c.Difficulty is not (null or "" or "Easy" or "Medium" or "Hard") || c.BloomLevel?.Length > 30)
                return ServiceResult<Question>.FailureResult("Nội dung câu hỏi không được trống; chọn độ khó hợp lệ.");
            Question question;
            if (questionId.HasValue)
            {
                var existing = detail.Data!.Questions.SingleOrDefault(x => x.QuestionId == questionId.Value);
                if (existing == null) return ServiceResult<Question>.FailureResult("Câu hỏi không thuộc môn học này.");
                if (detail.Data.LockedQuestionIds.Contains(questionId.Value))
                    return ServiceResult<Question>.FailureResult("Câu hỏi đã dùng trong kỳ thi công bố. Hãy thêm câu hỏi mới để giữ nguyên đề đã công bố.");
                question = existing;
            }
            else
            {
                question = new Question { CourseId = courseId };
                await uow.Questions.AddAsync(question);
            }
            question.QuestionText = c.Content.Trim();
            question.Difficulty = string.IsNullOrWhiteSpace(c.Difficulty) ? null : c.Difficulty;
            question.BloomLevel = string.IsNullOrWhiteSpace(c.BloomLevel) ? null : c.BloomLevel.Trim();
            question.IsApproved = c.IsApproved; question.IsActive = c.IsActive; question.UpdatedAt = DateTime.UtcNow;
            await uow.SaveChangesAsync();
            return ServiceResult<Question>.SuccessResult(question, "Đã lưu câu hỏi. Chỉ câu đang hoạt động và được duyệt mới xuất hiện khi chọn đề.");
        }, x => x.Success, courseId);
}
