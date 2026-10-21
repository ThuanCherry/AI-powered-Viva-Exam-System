using System.Text;
using AIVES.Api.Data;
using AIVES.Api.Features.Auth;
using AIVES.Api.Hubs;
using AIVES.Api.Integrations.LLM;
using AIVES.Api.Integrations.STT;
using AIVES.Api.Integrations.TTS;
using AIVES.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AIVES.Api.Features.Workflow;
using AIVES.Api.Integrations;
using AIVES.Api.Features.Lecturer;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true);
LocalConfiguration.AddLocalSecrets(builder.Configuration, builder.Environment);
builder.Configuration.AddEnvironmentVariables();

// Uses the exact runtime configuration, without starting workers or touching exam data.
if (args.Contains("--check-database"))
{
    await DatabaseDiagnostics.Check(builder.Configuration);
    return;
}

// Local diagnostic only: never starts the server, opens MySQL, or calls an AI provider.
if (args.Contains("--check-configuration"))
{
    var aiStatus = new ProviderStatus(builder.Configuration, builder.Environment);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
        environment = builder.Environment.EnvironmentName,
        apiKeyPresent = !string.IsNullOrWhiteSpace(builder.Configuration["AI:ApiKey"]),
        aiProvider = builder.Configuration["AI:Provider"],
        speechProvider = builder.Configuration["Speech:Provider"],
        providers = aiStatus.View
    }));
    return;
}

// ---------- Database ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AivesDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));

// ---------- Authentication (JWT) ----------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Allow SignalR to receive token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------- Services ----------
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<ScheduleNotifications>();
builder.Services.AddScoped<LecturerService>();
builder.Services.AddScoped<DocumentStore>();
builder.Services.AddScoped<VivaEngine>();
builder.Services.AddSingleton<ProviderStatus>();
builder.Services.AddHttpClient("providers");
builder.Services.AddSingleton<ProviderHttp>();
builder.Services.AddSingleton<GeminiModelCatalog>();
builder.Services.AddScoped<IVivaLlm, GeminiViva>();
builder.Services.AddSingleton<IGeminiSocketFactory, GeminiSocketFactory>();
builder.Services.AddSingleton<IGeminiLiveTranscriber, GeminiLiveTranscriber>();
builder.Services.AddSingleton<LiveStreamLimits>();
builder.Services.AddSingleton<LiveAttemptService>();
builder.Services.AddScoped<IVivaSpeech, GeminiVivaSpeech>();
builder.Services.AddHostedService<DeadlineWorker>();

// ---------- Controllers + SignalR ----------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSignalR(options => { options.MaximumReceiveMessageSize = 16384; options.StreamBufferCapacity = 10; });

// ---------- Swagger / OpenAPI ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "AIVES API", Version = "v1" });
});

// ---------- CORS ----------
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://localhost:5173"];
        origins = FrontendOrigins.Resolve(origins, builder.Environment.IsDevelopment());

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();
var configuredProviders = app.Services.GetRequiredService<ProviderStatus>();
if (!configuredProviders.Llm || !configuredProviders.Speech)
    app.Logger.LogWarning("AI configuration incomplete in {Environment}. Use --check-configuration to see missing field names; no provider request was made.", app.Environment.EnvironmentName);

// ---------- Middleware Pipeline ----------
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", async (AivesDbContext db, CancellationToken ct) =>
{
    try { return await db.Database.CanConnectAsync(ct) ? Results.Ok(new { database = "Connected" }) : Results.Json(new { database = "Unavailable" }, statusCode: 503); }
    catch { return Results.Json(new { database = "Unavailable" }, statusCode: 503); }
});
app.MapHub<ExamHub>("/hubs/exam");

app.Run();
