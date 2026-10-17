using System.Security.Claims;
using AIVES.BusinessLogicLayer.Common;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.PresentationLayer.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIVES.PresentationLayer.Controllers;
public class AccountController(IUserService users) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? Destination() : View(new LoginViewModel());
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await users.LoginAsync(model.Email, model.Password);
        if (!result.Success) { ModelState.AddModelError("", result.Message); return View(model); }
        await SignInAsync(result.Data!, model.RememberMe);
        return result.Data!.Roles.Contains("LECTURER") ? RedirectToAction("Index", "Exams") : RedirectToAction("MySchedule", "Student");
    }
    [AllowAnonymous, HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await users.RegisterAsync(new(model.FullName, model.Email, model.StudentCode, model.Password));
        if (!result.Success) { ModelState.AddModelError("", result.Message); return View(model); }
        TempData["Success"] = "Đăng ký thành công với role STUDENT. Hãy đăng nhập.";
        return RedirectToAction(nameof(Login));
    }
    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied() { Response.StatusCode = 403; return View(); }
    private IActionResult Destination() => User.IsInRole("LECTURER") ? RedirectToAction("Index", "Exams") : RedirectToAction("MySchedule", "Student");
    private Task SignInAsync(AuthUser user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()), new("UserId", user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName), new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(user.Roles.Select(x => new Claim(ClaimTypes.Role, x)));
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = remember });
    }
}