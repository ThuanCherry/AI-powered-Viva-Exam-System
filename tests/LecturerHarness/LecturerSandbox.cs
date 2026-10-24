using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using AIVES.Api.Controllers;
using AIVES.Api.Data;
using AIVES.Api.Features.Auth;
using AIVES.Api.Features;
using AIVES.Api.Features.Lecturer;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using AIVES.Api.Middleware;
using AIVES.Api.Models;
using AIVES.Api.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AIVES.Lecturer.Testing;

public sealed class NoAi : IVivaLlm, IVivaSpeech
{
    public int Calls;
    private Exception Denied() { Interlocked.Increment(ref Calls); return new WorkflowException(503, "AI bị vô hiệu hóa trong môi trường kiểm thử giảng viên."); }
    public Task<FollowUpDecision> Decide(string m, string e, string q, string t, string h, CancellationToken ct) => throw Denied();
    public Task<string> Transcribe(byte[] wav, CancellationToken ct) => throw Denied();
    public Task<byte[]> Speak(string text, CancellationToken ct) => throw Denied();
}
public sealed class NoExternalHttp : HttpMessageHandler, IHttpClientFactory
{
    public int Calls;
    public HttpClient CreateClient(string name) => new(this, false);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    { Interlocked.Increment(ref Calls); throw new InvalidOperationException("Mọi HTTP ngoại vi đều bị chặn trong sandbox giảng viên."); }
}

public sealed class LecturerSandbox : IAsyncDisposable
{
    public const string Password = "Lecturer-Sandbox-Only-123!";
    private const string JwtKey = "lecturer-sandbox-only-signing-key-at-least-32-characters";
    public WebApplication App { get; private set; } = null!;
    public HttpClient Http { get; private set; } = null!;
    public readonly NoAi Ai = new();
    public readonly NoExternalHttp Network = new();
    public string Storage { get; private set; } = "";
    private SqliteConnection connection = null!;
    private string? ownedTestDirectory;

    public static async Task<LecturerSandbox> Create(string? url = null, string? file = null)
    {
        var sandbox = new LecturerSandbox();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "LecturerTest", ApplicationName = typeof(CoursesController).Assembly.GetName().Name });
        // Never read User Secrets, appsettings, MySQL environment or vendor keys for these tests.
        builder.Configuration.Sources.Clear();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        sandbox.Storage = Path.GetFullPath(Path.Combine("artifacts", "lecturer-sandbox", file == null ? Guid.NewGuid().ToString("N") : "preview", "documents"));
        if (file == null) sandbox.ownedTestDirectory = Path.GetDirectoryName(sandbox.Storage);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:Key"] = JwtKey, ["Jwt:Issuer"] = "LecturerTest", ["Jwt:Audience"] = "LecturerTest",
            ["Documents:StorageRoot"] = sandbox.Storage, ["AI:Provider"] = "Disabled", ["Speech:Provider"] = "Disabled" });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        if (file != null) Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        sandbox.connection = new SqliteConnection(file == null ? "Data Source=:memory:" : new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(file) }.ToString());
        await sandbox.connection.OpenAsync();
        builder.Services.AddDbContext<AivesDbContext>(o => {
            if (file == null) o.UseSqlite(sandbox.connection);
            else o.UseSqlite(sandbox.connection.ConnectionString); // Separate connections for concurrent browser requests.
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddApplicationServices();
        builder.Services.AddSingleton<ProviderStatus>(); builder.Services.AddSingleton<ProviderHttp>(); builder.Services.AddSingleton<GeminiModelCatalog>();
        builder.Services.AddSingleton<IVivaLlm>(sandbox.Ai); builder.Services.AddSingleton<IVivaSpeech>(sandbox.Ai);
        builder.Services.AddSingleton<IHttpClientFactory>(sandbox.Network);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = "LecturerTest", ValidAudience = "LecturerTest", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)) });
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.Events.OnMessageReceived = context => {
            if (context.Request.Path.StartsWithSegments("/hubs") && context.Request.Query.TryGetValue("access_token", out var token)) context.Token = token;
            return Task.CompletedTask;
        });
        builder.Services.AddAuthorization(); builder.Services.AddSignalR();
        builder.Services.AddControllers().AddApplicationPart(typeof(CoursesController).Assembly);
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(FrontendOrigins.Resolve([], true)).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
        sandbox.App = builder.Build(); sandbox.App.UseMiddleware<ExceptionMiddleware>(); sandbox.App.UseCors(); sandbox.App.UseAuthentication(); sandbox.App.UseAuthorization(); sandbox.App.MapControllers();
        sandbox.App.MapHub<ExamHub>("/hubs/exam");
        sandbox.App.MapGet("/sandbox/status", () => new { database = "SQLiteTestOnly", aiCalls = sandbox.Ai.Calls, externalHttpCalls = sandbox.Network.Calls, productionConfigurationLoaded = false });
        // Notification hub is real; audio services are not registered. No worker or external HTTP.
        using (var scope = sandbox.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
            await db.Database.EnsureCreatedAsync(); // Guarded SQLite connection above, never MySQL.
            if (!await db.Users.AnyAsync())
            {
                db.Roles.AddRange(new Role { RoleId = 1, RoleName = "Lecturer" }, new Role { RoleId = 2, RoleName = "Student" }, new Role { RoleId = 3, RoleName = "Admin" });
                db.Users.AddRange(new User { UserId = 1, RoleId = 1, Email = "lecturer@lecturer-test.invalid", FullName = "Giảng viên kiểm thử", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) },
                    new User { UserId = 2, RoleId = 2, Email = "student1@lecturer-test.invalid", FullName = "Sinh viên kiểm thử 1", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) },
                    new User { UserId = 3, RoleId = 2, Email = "student2@lecturer-test.invalid", FullName = "Sinh viên kiểm thử 2", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) },
                    new User { UserId = 4, RoleId = 1, Email = "other@lecturer-test.invalid", FullName = "Giảng viên khác", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) });
                await db.SaveChangesAsync();
            }
            // Separate real Admin identity for permission tests; do not grant fake role claims to lecturers.
            if (!await db.Users.AnyAsync(u => u.Email == "admin@lecturer-test.invalid"))
            {
                var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
                if (adminRole == null) { adminRole = new Role { RoleName = "Admin" }; db.Roles.Add(adminRole); }
                db.Users.Add(new User { UserId = 100, Role = adminRole, Email = "admin@lecturer-test.invalid", FullName = "Quản trị viên kiểm thử",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
        }
        sandbox.App.Urls.Add(url ?? "http://127.0.0.1:0"); await sandbox.App.StartAsync();
        var address = sandbox.App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        sandbox.Http = new HttpClient { BaseAddress = new Uri(address) };
        return sandbox;
    }
    public string Token(int user = 1, string role = "Lecturer") => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "LecturerTest", "LecturerTest", [new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, role)],
        expires: DateTime.UtcNow.AddHours(4), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256)));
    public async ValueTask DisposeAsync()
    {
        Http.Dispose(); await App.StopAsync(); await App.DisposeAsync(); await connection.DisposeAsync(); Network.Dispose();
        // Only remove the uniquely generated directory owned by this in-memory test run.
        if (ownedTestDirectory != null && Directory.Exists(ownedTestDirectory))
        {
            var root = Path.GetFullPath(Path.Combine("artifacts", "lecturer-sandbox")) + Path.DirectorySeparatorChar;
            if (!ownedTestDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(ownedTestDirectory), "N", out _))
                throw new InvalidOperationException("Unsafe test cleanup path.");
            Directory.Delete(ownedTestDirectory, recursive: true);
        }
        if (Ai.Calls != 0 || Network.Calls != 0) throw new InvalidOperationException("Kiểm thử đã chạm tích hợp AI/ngoại vi bị cấm.");
    }
}
