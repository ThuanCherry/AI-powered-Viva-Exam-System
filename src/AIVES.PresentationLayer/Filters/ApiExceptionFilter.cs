using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AIVES.PresentationLayer.Filters;

public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception occurred: {Message}", context.Exception.Message);

        var result = new ObjectResult(new
        {
            success = false,
            message = "Đã xảy ra lỗi trong quá trình xử lý yêu cầu.",
            details = context.Exception.Message
        })
        {
            StatusCode = 500
        };

        context.Result = result;
        context.ExceptionHandled = true;
    }
}
