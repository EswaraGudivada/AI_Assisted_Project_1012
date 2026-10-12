namespace UrlShortener.Api;

using System.Text.Json;
using UrlShortener.Application;

/// <summary>
/// Middleware for handling exceptions and returning structured error responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
            await HandleExceptionAsync(context, exception);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new { error = string.Empty, code = string.Empty, statusCode = 0 };

        if (exception is UrlValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            response = new { error = "Invalid URL", code = "VALIDATION_ERROR", statusCode = 400 };
        }
        else if (exception is NotFoundException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            response = new { error = "Resource not found", code = "NOT_FOUND", statusCode = 404 };
        }
        else if (exception is CollisionExhaustionException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            response = new { error = "Unable to generate unique code", code = "COLLISION", statusCode = 409 };
        }
        else if (exception is ArgumentException or ArgumentNullException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            response = new { error = "Invalid request", code = "BAD_REQUEST", statusCode = 400 };
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            response = new { error = "Internal server error", code = "INTERNAL_ERROR", statusCode = 500 };
        }

        return context.Response.WriteAsJsonAsync(response);
    }
}
