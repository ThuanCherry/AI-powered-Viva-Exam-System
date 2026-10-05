using System.Security.Claims;
using AIVES.BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
[ApiController, Route("api/examsessions"), Authorize(Roles = "STUDENT")]
public class ExamSessionsController(IExamSessionService schedules) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine() => Ok(await schedules.GetMyScheduleAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
}