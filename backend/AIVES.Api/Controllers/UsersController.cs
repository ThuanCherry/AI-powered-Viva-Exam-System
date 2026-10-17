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
}
