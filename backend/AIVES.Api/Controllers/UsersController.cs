using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Features.Auth;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AuthService _authService;

    public UsersController(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>POST /api/Users/login</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new
        {
            success = true,
            data = new
            {
                userId = result.User!.UserId,
                fullName = result.User.FullName,
                email = result.User.Email,
                role = result.User.Role,
                token = result.Token
            }
        });
    }

    /// <summary>POST /api/Users/register</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new { success = true, message = "Registration successful." });
    }

    /// <summary>GET /api/Users?role=Student</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromServices] Data.AivesDbContext db, [FromQuery] string? role)
    {
        var query = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(db.Users, u => u.Role).AsQueryable();
        if (!string.IsNullOrEmpty(role))
        {
            query = query.Where(u => u.Role != null && u.Role.RoleName == role);
        }

        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            query.Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                Role = u.Role != null ? u.Role.RoleName : "Unknown",
                u.Status
            })
        );

        return Ok(new { success = true, data = users });
    }

    /// <summary>PUT /api/Users/{id} - Cập nhật thông tin & vai trò (Role) người dùng</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request, [FromServices] Data.AivesDbContext db)
    {
        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(db.Users, u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user is null)
            return NotFound(new { success = false, message = "User not found." });

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            user.Status = request.Status.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleName = request.Role.Trim();
            var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName.ToLower() == roleName.ToLower());
            if (role != null)
            {
                user.RoleId = role.RoleId;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Reload updated role
        await db.Entry(user).Reference(u => u.Role).LoadAsync();

        return Ok(new
        {
            success = true,
            message = "User updated successfully.",
            data = new
            {
                user.UserId,
                user.FullName,
                user.Email,
                Role = user.Role != null ? user.Role.RoleName : "Unknown",
                user.Status
            }
        });
    }

    /// <summary>PUT /api/Users/{id}/status - Khóa / Mở khóa tài khoản</summary>
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> ToggleStatus(int id, [FromServices] Data.AivesDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return NotFound(new { success = false, message = "User not found." });

        user.Status = user.Status == "Active" ? "Inactive" : "Active";
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { success = true, message = $"Status updated to {user.Status}.", status = user.Status });
    }
}

public record UpdateUserRequest(string? FullName, string? Role, string? Status);

