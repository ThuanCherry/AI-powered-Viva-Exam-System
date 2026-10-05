using System.Security.Claims;
using AIVES.BusinessLogicLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
[Authorize(Roles = "STUDENT")]
public class StudentController(IExamSessionService schedules) : Controller
{
    [HttpGet]
    public async Task<IActionResult> MySchedule() =>
        View(await schedules.GetMyScheduleAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
}