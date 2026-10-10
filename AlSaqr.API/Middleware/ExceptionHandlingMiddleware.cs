using AlSaqr.Domain.Common;
using AlSaqr.Infrastructure.Video;
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
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The client disconnected and the action observed its abort signal. That
                // is not a server error, and nobody is left to read a body: answer 499
                // (client closed request) with no ProblemDetails.
                _logger.LogInformation("Request {Path} was aborted by the client.", context.Request.Path);
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
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
                    // The participant's row exists but the SFU has no live connection for it.
                    ParticipantNotConnectedException => (StatusCodes.Status409Conflict, "Conflict"),
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
                    // bodies) that must not reach a client. The SFU message names the
                    // room, which is never sent to clients outside their own join token.
                    Detail = ex switch
                    {
                        ParticipantNotConnectedException => "The participant has no live video connection.",
                        _ when status == StatusCodes.Status500InternalServerError => "An unexpected error occurred.",
                        _ => ex.Message,
                    },
                    Instance = context.Request.Path,
                };

                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json");
            }
        }
    }
}
