using System.Security.Claims;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.PresentationLayer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
[Authorize(Roles = "LECTURER")]
public class CoursesController(ICourseService courses) : Controller
{
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<IActionResult> Index() => View(await courses.GetForLecturerAsync(CurrentUserId, true));
    [HttpGet("/api/courses")] public async Task<IActionResult> GetAll() => Ok(await courses.GetForLecturerAsync(CurrentUserId));
    [HttpGet] public IActionResult Create() => View(new CourseViewModel());
    [HttpPost] public async Task<IActionResult> Create(CourseViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await courses.CreateAsync(model.ToCommand(), CurrentUserId);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToAction(nameof(Details), new { id = result.Data!.CourseId }); }
            ModelState.AddModelError("", result.Message);
        }
        return View(model);
    }
    [HttpGet] public async Task<IActionResult> Details(long id)
    {
        var result = await courses.GetDetailsAsync(id, CurrentUserId);
        return result.Success ? View(result.Data) : Forbid();
    }
    [HttpGet] public async Task<IActionResult> Edit(long id)
    {
        var result = await courses.GetDetailsAsync(id, CurrentUserId);
        return result.Success ? View(CourseViewModel.From(result.Data!.Course)) : Forbid();
    }
    [HttpPost] public async Task<IActionResult> Edit(long id, CourseViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await courses.UpdateAsync(id, model.ToCommand(), CurrentUserId);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToAction(nameof(Details), new { id }); }
            ModelState.AddModelError("", result.Message);
        }
        return View(model);
    }
    [HttpPost] public async Task<IActionResult> Students(long id, ParticipantForm model)
    {
        var result = ModelState.IsValid ? await courses.SetStudentsAsync(id, model.StudentIds, CurrentUserId) : null;
        TempData[result?.Success == true ? "Success" : "Error"] = result?.Message ?? "Dữ liệu sinh viên không hợp lệ.";
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpGet] public async Task<IActionResult> Question(long id, long? questionId)
    {
        var detail = await courses.GetDetailsAsync(id, CurrentUserId);
        if (!detail.Success) return Forbid();
        if (!questionId.HasValue) return View(new QuestionViewModel());
        var question = detail.Data!.Questions.SingleOrDefault(x => x.QuestionId == questionId);
        if (question == null) return NotFound();
        if (detail.Data.LockedQuestionIds.Contains(questionId.Value)) { TempData["Error"] = "Câu hỏi đã dùng trong kỳ thi công bố. Hãy thêm câu hỏi mới."; return RedirectToAction(nameof(Details), new { id }); }
        return View(QuestionViewModel.From(question));
    }
    [HttpPost] public async Task<IActionResult> Question(long id, long? questionId, QuestionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await courses.SaveQuestionAsync(id, questionId, model.ToCommand(), CurrentUserId);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToAction(nameof(Details), new { id }); }
            ModelState.AddModelError("", result.Message);
        }
        return View(model);
    }
}