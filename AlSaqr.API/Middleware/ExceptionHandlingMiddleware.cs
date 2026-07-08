using AlSaqr.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Middleware
{
    /// <summary>
    /// Single global exception → HTTP mapping (CLAUDE.md §3.3). Repositories throw
    /// the custom exceptions; this middleware is the only place they become
    /// RFC 7807 ProblemDetails responses. Controllers never try/catch for HTTP
    /// mapping.
    /// </summary>
    public sealed class ExceptionHandlingMiddleware
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
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                    throw;

                // Single source of truth for the exception → status code mapping.
                var (status, title) = ex switch
                {
                    NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                    ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                    ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                    ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
                    _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
                };

                if (status == StatusCodes.Status500InternalServerError)
                    _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);

                var problem = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    // Unexpected errors keep their detail out of the response — the
                    // message may carry internals (connection strings, upstream
                    // bodies) that must not reach a client.
                    Detail = status == StatusCodes.Status500InternalServerError
                        ? "An unexpected error occurred."
                        : ex.Message,
                    Instance = context.Request.Path,
                };

                context.Response.StatusCode = status;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}
