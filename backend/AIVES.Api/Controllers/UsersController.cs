using AIVES.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Features.Auth;

[ApiController, Route("api/[controller]")]
public class UsersController(AuthService auth, UserManagementService users) : ApiControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await auth.LoginAsync(request);
        if (!result.Success) return BadRequest(new { success = false, message = result.Message });
        return Data(new { userId = result.User!.UserId, fullName = result.User.FullName,
            email = result.User.Email, role = result.User.Role, token = result.Token });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await auth.RegisterAsync(request);
        if (!result.Success) return BadRequest(new { success = false, message = result.Message });
        return Ok(new { success = true, message = "Registration successful." });
    }

    [HttpGet, Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetAll([FromQuery] string? role, CancellationToken ct) =>
        Data(await users.ListAsync(Caller, role, ct));

    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        Ok(new { success = true, message = "User updated successfully.", data = await users.UpdateAsync(id, request, Caller, ct) });

    [HttpPut("{id:int}/status"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken ct)
    {
        var status = await users.ToggleStatusAsync(id, Caller, ct);
        return Ok(new { success = true, message = $"Status updated to {status}.", status });
    }
}
