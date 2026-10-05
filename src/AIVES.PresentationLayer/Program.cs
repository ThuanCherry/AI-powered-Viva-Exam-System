using System.Security.Claims;
using AIVES.BusinessLogicLayer;
using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.BusinessLogicLayer.Services;
using AIVES.PresentationLayer.Filters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAivesLayers(builder.Configuration);
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AutoValidateAntiforgeryTokenAttribute>();
    options.Filters.Add<ApiExceptionFilter>();
});
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "AIVES.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnValidatePrincipal = async context =>
    {
        var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var identity = long.TryParse(idText, out var id) ?
            await context.HttpContext.RequestServices.GetRequiredService<IUserService>().GetIdentityAsync(id) : null;
        var claimRoles = context.Principal?.FindAll(ClaimTypes.Role).Select(x => x.Value).Order().ToArray() ?? [];
        if (identity == null || !identity.Roles.Order().SequenceEqual(claimRoles))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
});
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().RequireAuthorization();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.Run();
public partial class Program { }
