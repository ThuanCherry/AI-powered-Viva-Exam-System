using Microsoft.AspNetCore.Mvc;

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
}
