using employee.management.identity.core.Exceptions;
using employee.management.identity.models.ErrorModels;
using System.Text.Json;

namespace employee.management.identity.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            ErrorDetails errorDetails = new();

            switch(exception)
            {
                case FailedAuthenticationException:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    errorDetails.StatusCode = StatusCodes.Status401Unauthorized;
                    errorDetails.Message = exception.Message;
                    break;
                case UserNotFoundException:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    errorDetails.StatusCode = StatusCodes.Status404NotFound;
                    errorDetails.Message = exception.Message;
                    break;
                case BadRequestException:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    errorDetails.StatusCode = StatusCodes.Status400BadRequest;
                    errorDetails.Message = exception.Message;
                    break;
                case UnauthorizedAccessException:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    errorDetails.StatusCode = StatusCodes.Status401Unauthorized;
                    errorDetails.Message = exception.Message;
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    errorDetails.StatusCode = StatusCodes.Status500InternalServerError;
                    errorDetails.Message = "Internal Server Error.";
                    break;
            }

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorDetails));
        }
    }
}
