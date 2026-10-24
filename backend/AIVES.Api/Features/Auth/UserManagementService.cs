using AIVES.Api.Data;
using AIVES.Api.Features.Common;
using Microsoft.EntityFrameworkCore;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Features.Auth;

public record UpdateUserRequest(string? FullName, string? Role, string? Status);

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
            u.Status
        }).ToListAsync(ct);
    }

    public async Task<object> UpdateAsync(int id, UpdateUserRequest request, Caller caller, CancellationToken ct)
    {
        Require(caller.IsAdmin, "Chỉ quản trị viên được cập nhật tài khoản.", 403);
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id, ct);
        Require(user != null, "User not found.", 404);
        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Status)) user.Status = request.Status.Trim();
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleName = request.Role.Trim();
            var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName.ToLower() == roleName.ToLower(), ct);
            if (role != null) user.RoleId = role.RoleId;
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
        var user = await db.Users.FindAsync([id], ct);
        Require(user != null, "User not found.", 404);
        user.Status = user.Status == "Active" ? "Inactive" : "Active";
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return user.Status;
    }
}
