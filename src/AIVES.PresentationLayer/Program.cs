using AIVES.BusinessLogicLayer;
using AIVES.PresentationLayer.Filters;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. Khối Nối Tầng - Đăng ký DI của Business Layer & Data Access Layer =====
builder.Services.AddAivesLayers(builder.Configuration);

// ===== 2. Presentation Layer (WebMVC) Configuration =====
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
});

// Swagger / OpenAPI
builder.Services.AddOpenApi();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== 3. Pipeline / Middleware =====
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

// Route cho MVC Controllers (Razor Views)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Route cho API Controllers
app.MapControllers();

app.Run();
