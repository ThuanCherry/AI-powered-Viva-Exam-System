using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Auth;

public record UpdateUserRequest(string? FullName, string? Role, string? Status);
public record CreateUserRequest(string FullName, string Email, string Password, string Role);

public class UserManagementService(AivesDbContext db)
{
    public async Task<object> ListAsync(Caller caller, string? role, CancellationToken ct)
    {
        // A lecturer can only use this directory to select students.
        if (!caller.IsAdmin) role = "Student";
        var query = db.Users.Include(u => u.Role).AsQueryable();
        if (!string.IsNullOrEmpty(role)) query = query.Where(u => u.Role != null && u.Role.RoleName == role);
        return await query.Select(u => new
        {
            u.UserId,
            u.FullName,
            u.Email,
            Role = u.Role != null ? u.Role.RoleName : "Unknown",
            u.Status,
            u.CreatedAt
        }).ToListAsync(ct);
    }

    public async Task<object> CreateAsync(CreateUserRequest request, Caller caller, CancellationToken ct)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được tạo tài khoản.", 403);
        var name = request.FullName?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        Require(name is { Length: > 0 and <= 150 }, "Họ tên cần từ 1–150 ký tự.");
        Require(email is { Length: > 0 and <= 255 } && new EmailAddressAttribute().IsValid(email), "Email không hợp lệ.");
        Require(request.Password is { Length: >= 8 } && Encoding.UTF8.GetByteCount(request.Password) <= 72,
            "Mật khẩu cần ít nhất 8 ký tự và tối đa 72 byte UTF-8.");
        var role = await ResolveRole(request.Role, ct);
        Require(!await db.Users.AnyAsync(u => u.Email.ToLower() == email, ct), "Email đã được sử dụng.", 409);
        var user = new Models.User { FullName = name, Email = email, RoleId = role.RoleId, Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        { throw new Workflow.WorkflowException(409, "Email đã được sử dụng."); }
        return new { user.UserId, user.FullName, user.Email, Role = role.RoleName, user.Status, user.CreatedAt };
    }

    public async Task<object> UpdateAsync(int id, UpdateUserRequest request, Caller caller, CancellationToken ct)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được cập nhật tài khoản.", 403);
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id, ct);
        Require(user != null, "User not found.", 404);
        if (request.FullName != null)
        {
            Require(request.FullName.Trim() is { Length: > 0 and <= 150 }, "Họ tên cần từ 1–150 ký tự.");
            user.FullName = request.FullName.Trim();
        }
        if (request.Status != null)
        {
            Require(request.Status is "Active" or "Inactive", "Trạng thái tài khoản không hợp lệ.");
            Require(user.Role?.RoleName != "Admin" || request.Status == "Active", "Không được khóa tài khoản quản trị viên.", 409);
            user.Status = request.Status;
        }
        if (request.Role != null)
        {
            var role = await ResolveRole(request.Role, ct);
            Require(user.Role?.RoleName != "Admin" || role.RoleName == "Admin", "Không được hạ quyền quản trị viên.", 409);
            if (role.RoleId != user.RoleId)
            {
                Require(!await HasAcademicData(id, ct), "Tài khoản đang gắn với môn, lớp hoặc kỳ thi; cần chuyển giao trước khi đổi vai trò.", 409);
                user.RoleId = role.RoleId;
                user.Role = role;
            }
        }
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await db.Entry(user).Reference(u => u.Role).LoadAsync(ct);
        return new
        {
            user.UserId,
            user.FullName,
            user.Email,
            Role = user.Role != null ? user.Role.RoleName : "Unknown",
            user.Status
        };
    }

    public async Task<string> ToggleStatusAsync(int id, Caller caller, CancellationToken ct)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được khóa/mở tài khoản.", 403);
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id, ct);
        Require(user != null, "User not found.", 404);
        Require(user.Role?.RoleName != "Admin", "Không được khóa tài khoản quản trị viên.", 409);
        user.Status = user.Status == "Active" ? "Inactive" : "Active";
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return user.Status;
    }

    private async Task<Models.Role> ResolveRole(string? value, CancellationToken ct)
    {
        Require(value is "Admin" or "Lecturer" or "Student", "Vai trò không hợp lệ.");
        var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleName == value, ct);
        Require(role != null, "Vai trò chưa được cấu hình trong database.", 409);
        return role;
    }

    private async Task<bool> HasAcademicData(int id, CancellationToken ct) =>
        await db.Courses.AnyAsync(c => c.LecturerId == id, ct)
        || await db.AcademicClasses.AnyAsync(c => c.LecturerId == id, ct)
        || await db.ExamSessions.AnyAsync(e => e.LecturerId == id, ct)
        || await db.ClassStudents.AnyAsync(s => s.StudentId == id, ct)
        || await db.ExamCandidates.AnyAsync(c => c.StudentId == id, ct);
}
