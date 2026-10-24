using System.Security.Claims;
using AIVES.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Features.Auth;

public class AccountSessionValidator(AivesDbContext db)
{
    public async Task<bool> IsCurrentAsync(ClaimsPrincipal? principal, CancellationToken ct)
    {
        if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var role = principal?.FindFirstValue(ClaimTypes.Role);
        return await db.Users.AsNoTracking().AnyAsync(u => u.UserId == id && u.Status == "Active"
            && u.Role != null && u.Role.RoleName == role, ct);
    }

    public static async Task ValidateToken(TokenValidatedContext context)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<AccountSessionValidator>();
        if (!await validator.IsCurrentAsync(context.Principal, context.HttpContext.RequestAborted))
            context.Fail("Tài khoản đã bị khóa hoặc vai trò đã thay đổi. Vui lòng đăng nhập lại.");
    }
}
