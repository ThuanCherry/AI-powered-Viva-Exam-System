using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using AIVES.Api.Features.Workflow;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Catalog;

public class CourseCatalogService(AivesDbContext _db)
{
    public async Task<object> ListAsync(Caller caller, int? lecturerId, CancellationToken ct)
    {
        var user = caller.UserId;
        var teacher = caller.IsLecturer;
        var courses = await _db.Courses
            .Include(c => c.Lecturer)
            .Where(c => c.IsActive && (caller.IsAdmin ? !lecturerId.HasValue || c.LecturerId == lecturerId : teacher ? c.LecturerId == user : _db.ClassStudents.Any(s => s.StudentId == user && s.AcademicClass!.CourseId == c.CourseId && s.AcademicClass.IsActive)))
            .OrderBy(c => c.CourseName)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.LecturerId,
                LecturerName = c.Lecturer!.FullName,
                c.CreatedAt
            })
            .ToListAsync(ct);

        return courses;
    }
    public async Task<object> DetailAsync(int id, Caller caller, CancellationToken ct)
    {
        var user = caller.UserId;
        var course = await _db.Courses
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.CourseId == id, ct);

        Require(course != null, "Không tìm thấy môn học.", 404);
        var allowed = caller.IsAdmin || (caller.IsLecturer ? course.LecturerId == user
            : await _db.ClassStudents.AnyAsync(s => s.StudentId == user && s.AcademicClass!.CourseId == id && s.AcademicClass.IsActive, ct));
        Require(allowed, "Bạn không có quyền truy cập môn học.", 403);

        return new
            {
                course.CourseId,
                course.CourseCode,
                course.CourseName,
                course.Description,
                course.IsActive,
                LecturerName = course.Lecturer?.FullName,
                course.CreatedAt
            };
    }
    public async Task<object> CreateCourseAsync(Controllers.CreateCourseAdminRequest request, Caller caller, CancellationToken ct)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được tạo môn học.", 403);
        var code = request.CourseCode?.Trim().ToUpperInvariant() ?? "";
        Require(System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9][A-Z0-9._-]{0,49}$"), "Mã môn gồm 1–50 ký tự chữ, số, dấu chấm/gạch ngang/gạch dưới.");
        var name = request.CourseName?.Trim() ?? "";
        Require(name is { Length: > 0 and <= 200 }, "Tên môn cần từ 1–200 ký tự.");
        Require(request.Description?.Length is not > 1000, "Mô tả tối đa 1000 ký tự.");
        Require(!await _db.Courses.AnyAsync(c => c.CourseCode.ToUpper() == code, ct), "Mã môn học đã tồn tại.", 409);

        int lecturerId;
        if (request.LecturerId.HasValue)
        {
            var exists = await _db.Users.AnyAsync(u => u.UserId == request.LecturerId.Value && u.Status == "Active" && u.Role!.RoleName == "Lecturer", ct);
            Require(exists, "Giảng viên được chọn không tồn tại hoặc không hoạt động.", 400);
            lecturerId = request.LecturerId.Value;
        }
        else
        {
            var firstLecturer = await _db.Users.Where(u => u.Status == "Active" && u.Role!.RoleName == "Lecturer").Select(u => (int?)u.UserId).FirstOrDefaultAsync(ct);
            Require(firstLecturer.HasValue, "Hệ thống chưa có giảng viên hoạt động để phân công môn học. Vui lòng tạo tài khoản giảng viên trước.", 400);
            lecturerId = firstLecturer.Value;
        }

        var course = new Models.Course
        {
            CourseCode = code,
            CourseName = name,
            Description = request.Description?.Trim(),
            LecturerId = lecturerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(course).Reference(c => c.Lecturer).LoadAsync(ct);
        return new { course.CourseId, course.CourseCode, course.CourseName, course.LecturerId, LecturerName = course.Lecturer?.FullName };
    }

    public async Task<object> AssignLecturerAsync(int id, int? lecturerId, Caller caller, CancellationToken ct)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được phân công giảng viên.", 403);
        var course = await _db.Courses.Include(c => c.Lecturer).FirstOrDefaultAsync(c => c.CourseId == id, ct);
        Require(course != null, "Không tìm thấy môn học.", 404);
        if (!lecturerId.HasValue || !await _db.Users.AnyAsync(u => u.UserId == lecturerId && u.Status == "Active" && u.Role!.RoleName == "Lecturer", ct))
            throw new WorkflowException(400, "Chọn giảng viên đang hoạt động.");
        if (course.LecturerId != lecturerId && (await _db.AcademicClasses.AnyAsync(c => c.CourseId == id, ct) || await _db.ExamSessions.AnyAsync(e => e.CourseId == id, ct)))
            throw new WorkflowException(409, "Môn đã có lớp hoặc kỳ thi; cần chuyển giao nghiệp vụ trước khi đổi giảng viên.");
        course.LecturerId = lecturerId.Value; course.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); await _db.Entry(course).Reference(c => c.Lecturer).LoadAsync(ct);
        return new { course.CourseId, course.CourseCode, course.CourseName, course.LecturerId, LecturerName = course.Lecturer?.FullName };
    }
}
