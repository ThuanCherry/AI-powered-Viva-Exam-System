using System.Net;
using System.Text.Json;
using AIVES.Api.Features.Workflow;

namespace AIVES.Api.Middleware;

/// <summary>
/// Global exception handler that returns consistent JSON error responses.
/// Matches the { success: false, message, details } format expected by the frontend.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (WorkflowException ex)
        {
            context.Response.StatusCode = ex.Status;
            await context.Response.WriteAsJsonAsync(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = "Có lỗi khi xử lý yêu cầu. Vui lòng thử lại hoặc liên hệ người phụ trách.",
                details = (string?)null
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
