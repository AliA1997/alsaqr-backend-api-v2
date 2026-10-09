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
using Xunit;

namespace AlSaqr.Tests.Middleware
{
    public class ExceptionHandlingMiddlewareTests
    {
        private static async Task<HttpResponseMessage> SendThrowing(Exception toThrow)
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services => services.AddLogging())
                    .Configure(app =>
                    {
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        app.Run(_ => throw toThrow);
                    }))
                .StartAsync();

            return await host.GetTestClient().GetAsync("/api/anything");
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
