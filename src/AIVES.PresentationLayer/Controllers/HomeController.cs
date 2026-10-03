using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.PresentationLayer.Controllers;

public class HomeController : Controller
{
    private readonly IExamService _examService;
    private readonly ICourseService _courseService;

    public HomeController(IExamService examService, ICourseService courseService)
    {
        _examService = examService;
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var examsResult = await _examService.GetAllAsync();
        var coursesResult = await _courseService.GetAllAsync();

        ViewBag.Exams = examsResult.Data ?? Enumerable.Empty<Exam>();
        ViewBag.Courses = coursesResult.Data ?? Enumerable.Empty<Course>();

        return View();
    }
}
