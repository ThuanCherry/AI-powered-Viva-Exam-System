using System.Security.Claims;
using AIVES.BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
[ApiController, Route("api/users"), Authorize]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me() => Ok(await users.GetIdentityAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
}