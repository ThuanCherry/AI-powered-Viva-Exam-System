using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
public class HomeController : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole("LECTURER")) return RedirectToAction("Index", "Exams");
        if (User.IsInRole("STUDENT")) return RedirectToAction("MySchedule", "Student");
        return View();
    }
    [AllowAnonymous] public IActionResult Error() { Response.StatusCode = 500; return View(); }
}