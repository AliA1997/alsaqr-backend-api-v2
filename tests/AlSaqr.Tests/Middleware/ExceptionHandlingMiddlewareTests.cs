using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AlSaqr.API.Middleware;
using AlSaqr.Domain.Common;
using AlSaqr.Infrastructure.Video;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AlSaqr.Tests.Middleware
{
    public class ExceptionHandlingMiddlewareTests
    {
        private static Task<HttpResponseMessage> SendThrowing(Exception toThrow) =>
            Send(_ => throw toThrow, new CapturingLoggerProvider());

        private static async Task<HttpResponseMessage> Send(RequestDelegate endpoint, CapturingLoggerProvider logs)
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services => services.AddLogging(b => b.AddProvider(logs)))
                    .Configure(app =>
                    {
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        app.Run(endpoint);
                    }))
                .StartAsync();

            return await host.GetTestClient().GetAsync("/api/anything");
        }

        [Fact]
        public async Task CancellationFromClientAbort_Returns499WithoutBody_AndLogsNoError()
        {
            var logs = new CapturingLoggerProvider();

            var response = await Send(context =>
            {
                // The client disconnected, and the action observed its abort signal.
                context.RequestAborted = new CancellationToken(canceled: true);
                throw new OperationCanceledException(context.RequestAborted);
            }, logs);

            Assert.Equal(StatusCodes.Status499ClientClosedRequest, (int)response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
            Assert.DoesNotContain(logs.Levels, level => level >= LogLevel.Error);
        }

        [Fact]
        public async Task CancellationWithoutClientAbort_IsStillA500()
        {
            var logs = new CapturingLoggerProvider();

            // e.g. an upstream HttpClient timeout: a server-side failure, not the client leaving.
            var response = await Send(_ => throw new TaskCanceledException("upstream timed out"), logs);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains(LogLevel.Error, logs.Levels);
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<LogLevel> _levels = new();

            public IReadOnlyCollection<LogLevel> Levels => _levels.ToArray();

            public ILogger CreateLogger(string categoryName) => new Logger(_levels);

            public void Dispose() { }

            private sealed class Logger : ILogger
            {
                private readonly ConcurrentQueue<LogLevel> _levels;

                public Logger(ConcurrentQueue<LogLevel> levels) => _levels = levels;

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter) => _levels.Enqueue(logLevel);
            }
        }

        public static TheoryData<Exception, HttpStatusCode> MappedExceptions => new()
        {
            { new NotFoundException("missing"), HttpStatusCode.NotFound },
            { new ValidationException("bad input"), HttpStatusCode.BadRequest },
            { new ForbiddenException("not yours"), HttpStatusCode.Forbidden },
            { new ConflictException("already live"), HttpStatusCode.Conflict },
        };

        [Theory]
        [MemberData(nameof(MappedExceptions))]
        public async Task MappedException_ReturnsProblemDetailsWithStatusAndMessage(
            Exception exception, HttpStatusCode expected)
        {
            var response = await SendThrowing(exception);

            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal((int)expected, problem!.Status);
            Assert.Equal(exception.Message, problem.Detail);
            Assert.Equal("/api/anything", problem.Instance);
        }

        [Fact]
        public async Task ParticipantNotConnected_Returns409ProblemDetails_WithoutRoomName()
        {
            var response = await SendThrowing(new ParticipantNotConnectedException("stream-secret-room"));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(409, problem!.Status);
            Assert.Equal("The participant has no live video connection.", problem.Detail);
            Assert.DoesNotContain("stream-secret-room", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UnexpectedException_Returns500_AndHidesMessage()
        {
            var response = await SendThrowing(new InvalidOperationException("conn=secret-host"));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("An unexpected error occurred.", problem!.Detail);
            Assert.DoesNotContain("secret-host", await response.Content.ReadAsStringAsync());
        }
    }
}
