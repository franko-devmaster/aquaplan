using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AquaPlan.Api.Middleware;

public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    private readonly ILogger<ApiExceptionFilterAttribute> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionFilterAttribute(ILogger<ApiExceptionFilterAttribute> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public override void OnException(ExceptionContext context)
    {
        // Map known business exceptions to appropriate HTTP status codes
        if (context.Exception is InvalidOperationException)
        {
            _logger.LogWarning(context.Exception, "Business rule violation: {Message}", context.Exception.Message);

            context.Result = new ObjectResult(new { error = context.Exception.Message })
            {
                StatusCode = StatusCodes.Status400BadRequest,
            };
            context.ExceptionHandled = true;
            return;
        }

        if (context.Exception is KeyNotFoundException)
        {
            context.Result = new NotFoundResult();
            context.ExceptionHandled = true;
            return;
        }

        _logger.LogError(context.Exception, "Unhandled exception: {Message}", context.Exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };

        if (_environment.IsDevelopment())
        {
            problemDetails.Detail = context.Exception.ToString();
        }

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status500InternalServerError,
        };

        context.ExceptionHandled = true;
    }
}
