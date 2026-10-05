using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace AIVES.PresentationLayer.Filters;
public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // Do not expose SQL, connection strings, credentials or hashes in responses/logs.
        logger.LogError("Request failed with {ExceptionType}", context.Exception.GetType().Name);
        context.Result = new ObjectResult(new { success = false, message = "Không thể xử lý yêu cầu. Kiểm tra cấu hình database hoặc liên hệ quản trị." }) { StatusCode = 500 };
        context.ExceptionHandled = true;
    }
}