using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using AIVES.DataAccessLayer.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace AIVES.BusinessLogicLayer.Services;
public class UserService(IUnitOfWork uow, IPasswordHasher<User> hasher) : IUserService
{
    public async Task<ServiceResult<AuthUser>> RegisterAsync(RegisterRequest r)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(r.FullName) || r.FullName.Trim().Length > 255 ||
            email.Length > 255 || !System.Net.Mail.MailAddress.TryCreate(email, out _) ||
            r.Password.Length < 8 || r.Password.Length > 128 || !r.Password.Any(char.IsLetter) || !r.Password.Any(char.IsDigit) ||
            r.StudentCode?.Length > 50)
            return ServiceResult<AuthUser>.FailureResult("Thông tin đăng ký không hợp lệ. Mật khẩu cần ít nhất 8 ký tự, có chữ và số.");
        if (await uow.Users.ExistsAsync(x => x.Email == email))
            return ServiceResult<AuthUser>.FailureResult("Email đã được đăng ký.");
        var code = string.IsNullOrWhiteSpace(r.StudentCode) ? null : r.StudentCode.Trim();
        if (code != null && await uow.Users.ExistsAsync(x => x.StudentCode == code))
            return ServiceResult<AuthUser>.FailureResult("Mã sinh viên đã được sử dụng.");
        try
        {
            return await uow.InTransactionAsync(null, async () =>
            {
                var role = (await uow.Roles.FindAsync(x => x.Code == "STUDENT")).SingleOrDefault();
                if (role == null) return ServiceResult<AuthUser>.FailureResult("Database chưa có role STUDENT.");
                var user = new User { Email = email, FullName = r.FullName.Trim(), StudentCode = code, Status = "ACTIVE" };
                user.PasswordHash = hasher.HashPassword(user, r.Password);
                await uow.Users.AddAsync(user);
                await uow.SaveChangesAsync();
                await uow.UserRoles.AddAsync(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
                await uow.SaveChangesAsync();
                return ServiceResult<AuthUser>.SuccessResult(new(user.UserId, user.FullName, user.Email, new[] { "STUDENT" }));
            }, x => x.Success);
        }
        catch (DbUpdateException)
        {
            // The database unique constraints also protect concurrent registrations.
            return ServiceResult<AuthUser>.FailureResult("Không thể đăng ký. Kiểm tra email và mã sinh viên có bị trùng không.");
        }
    }

    public async Task<ServiceResult<AuthUser>> LoginAsync(string email, string password)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = (await uow.Users.FindAsync(x => x.Email == normalized)).SingleOrDefault();
        if (user == null || user.Status != "ACTIVE" || user.LockoutEndAt > DateTime.UtcNow)
            return ServiceResult<AuthUser>.FailureResult("Email hoặc mật khẩu không hợp lệ, hoặc tài khoản không hoạt động.");
        PasswordVerificationResult verified;
        try { verified = hasher.VerifyHashedPassword(user, user.PasswordHash, password); }
        catch (FormatException) { verified = PasswordVerificationResult.Failed; }
        if (verified == PasswordVerificationResult.Failed)
            return ServiceResult<AuthUser>.FailureResult("Email hoặc mật khẩu không hợp lệ.");
        var identity = await GetIdentityAsync(user.UserId);
        if (identity == null || identity.Roles.Count == 0) return ServiceResult<AuthUser>.FailureResult("Tài khoản chưa có quyền truy cập.");
        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync();
        return ServiceResult<AuthUser>.SuccessResult(identity);
    }
    public async Task<AuthUser?> GetIdentityAsync(long userId)
    {
        var user = await uow.Users.GetByIdAsync(userId);
        if (user == null || user.Status != "ACTIVE") return null;
        var links = await uow.UserRoles.FindAsync(x => x.UserId == userId);
        var ids = links.Select(x => x.RoleId).ToArray();
        var roles = await uow.Roles.FindAsync(x => ids.Contains(x.RoleId));
        return new(user.UserId, user.FullName, user.Email, roles.Select(x => x.Code).ToArray());
    }
}
