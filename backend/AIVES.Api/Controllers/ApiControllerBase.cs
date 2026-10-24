using System.Security.Claims;
using AIVES.Api.Features.Common;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected Caller Caller => new(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        User.FindFirstValue(ClaimTypes.Role) ?? "");
    protected int UserId => Caller.UserId;
    protected IActionResult Data(object? data) => Ok(new { success = true, data });
}
