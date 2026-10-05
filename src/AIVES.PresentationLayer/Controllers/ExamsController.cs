using System.Security.Claims;
using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.PresentationLayer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
[Authorize(Roles = "LECTURER")]
public class ExamsController(IExamService exams, ICourseService courses, IExamSessionService schedules, IQuestionAssignmentService assignments) : Controller
{
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public async Task<IActionResult> Index() => View(await exams.GetAllAsync(CurrentUserId));
    [HttpGet("/api/exams")] public async Task<IActionResult> ApiList() => Ok(await exams.GetAllAsync(CurrentUserId));
    [HttpGet] public async Task<IActionResult> Create()
    {
        return View(new CreateExamViewModel { Courses = await courses.GetForLecturerAsync(CurrentUserId) });
    }
    [HttpPost] public async Task<IActionResult> Create(CreateExamViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await exams.CreateAsync(model.ToCommand(), CurrentUserId);
            if (result.Success) return RedirectToAction(nameof(Details), new { id = result.Data!.ExamId });
            ModelState.AddModelError("", result.Message);
        }
        model.Courses = await courses.GetForLecturerAsync(CurrentUserId);
        return View(model);
    }
    [HttpGet] public async Task<IActionResult> Edit(long id)
    {
        var result = await exams.GetOwnedAsync(id, CurrentUserId, true);
        if (!result.Success) return Forbid();
        var model = CreateExamViewModel.From(result.Data!);
        model.Courses = await courses.GetForLecturerAsync(CurrentUserId);
        return View(model);
    }
    [HttpPost] public async Task<IActionResult> Edit(long id, CreateExamViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await exams.UpdateAsync(id, model.ToCommand(), CurrentUserId);
            if (result.Success) { TempData["Success"] = result.Message; return RedirectToAction(nameof(Details), new { id }); }
            ModelState.AddModelError("", result.Message);
        }
        model.Courses = await courses.GetForLecturerAsync(CurrentUserId);
        return View(model);
    }
    [HttpPost] public async Task<IActionResult> Delete(long id)
    {
        var result = await exams.DeleteAsync(id, CurrentUserId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
    [HttpGet] public async Task<IActionResult> Details(long id)
    {
        var result = await exams.GetDetailsAsync(id, CurrentUserId);
        return result.Success ? View(result.Data) : Forbid();
    }
    [HttpPost] public async Task<IActionResult> Participants(long id, ParticipantForm model) =>
        Finish(id, ModelState.IsValid ? await exams.SetParticipantsAsync(id, model.StudentIds, CurrentUserId) : Invalid());
    [HttpPost] public async Task<IActionResult> GenerateSchedule(long id, ScheduleSettingsViewModel model) =>
        Finish(id, ModelState.IsValid ? await schedules.GenerateScheduleAsync(id, CurrentUserId, model.StartsAt, model.DurationMinutesPerStudent) : Invalid());
    [HttpPost] public async Task<IActionResult> SaveSchedule(long id, ScheduleViewModel model) =>
        Finish(id, ModelState.IsValid ? await schedules.UpdateSlotsAsync(id, model.Slots.Select(x => new SlotCommand(x.ParticipantId, x.Start, x.End)).ToArray(), CurrentUserId) : Invalid());
    [HttpPost] public async Task<IActionResult> QuestionPool(long id, QuestionPoolForm model) =>
        Finish(id, ModelState.IsValid ? await exams.SetQuestionPoolAsync(id, model.QuestionIds, CurrentUserId) : Invalid());
    [HttpPost] public async Task<IActionResult> GenerateAssignments(long id) => Finish(id, await assignments.GenerateAsync(id, CurrentUserId));
    [HttpPost] public async Task<IActionResult> Publish(long id) => Finish(id, await exams.PublishAsync(id, CurrentUserId));
    private IActionResult Finish(long id, ServiceResult<bool> result)
    {
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
    private static ServiceResult<bool> Invalid() => ServiceResult<bool>.FailureResult("Dữ liệu form không hợp lệ.");
}
