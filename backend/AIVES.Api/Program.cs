using System.Text;
using AIVES.Api.Data;
using AIVES.Api.Features.Auth;
using AIVES.Api.Features.Exams;
using AIVES.Api.Hubs;
using AIVES.Api.Integrations.LLM;
using AIVES.Api.Integrations.STT;
using AIVES.Api.Integrations.TTS;
using AIVES.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AivesDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

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
builder.Services.AddScoped<ExamService>();
builder.Services.AddSingleton<ILlmService, PlaceholderLlmService>();
builder.Services.AddSingleton<ISttService, PlaceholderSttService>();
builder.Services.AddSingleton<ITtsService, PlaceholderTtsService>();

// ---------- Controllers + SignalR ----------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSignalR();

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

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

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
app.MapHub<ExamHub>("/hubs/exam");

app.Run();
