using AIVES.Api.Features.Auth;
using AIVES.Api.Features.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Controllers;

[ApiController, Route("api/Admin"), Authorize(Roles = "Admin")]
public class AdminController(UserManagementService users, AdminSettingsService settings) : ApiControllerBase
{
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct) =>
        Data(await users.CreateAsync(request, Caller, ct));

    [HttpGet("settings")]
    public IActionResult Settings() => Data(settings.Read(Caller));
}
