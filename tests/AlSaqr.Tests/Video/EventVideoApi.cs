using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using AlSaqr.API.Controllers.Meetup;
using AlSaqr.API.Extensions;
using AlSaqr.API.Middleware;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// The real controller, repository, verifier and middleware over the PostgREST
    /// fixture, with the SFU and Realtime faked.
    /// </summary>
    public sealed class EventVideoApi : IAsyncDisposable
    {
        public const string TokenSecret = "test-access-token-secret-0123456789-abcdefghij";

        private readonly IHost _host;
        private readonly StrongBox<bool> _abortRequests;

        private EventVideoApi(
            IHost host, FakeVideoSfuService sfu, FakeEventVideoBroadcaster broadcaster, StrongBox<bool> abortRequests)
        {
            _host = host;
            _abortRequests = abortRequests;
            Sfu = sfu;
            Broadcaster = broadcaster;
            Http = host.GetTestClient();
        }

        public HttpClient Http { get; }

        public FakeVideoSfuService Sfu { get; }

        public FakeEventVideoBroadcaster Broadcaster { get; }

        /// <summary>
        /// When true, every request reaches the controller with RequestAborted already
        /// cancelled, as when the client disconnected (or a keepalive fetch was cut off).
        /// </summary>
        public bool AbortRequests
        {
            get => _abortRequests.Value;
            set => _abortRequests.Value = value;
        }

        public static async Task<EventVideoApi> StartAsync(Supabase.Client supabase)
        {
            var sfu = new FakeVideoSfuService();
            var broadcaster = new FakeEventVideoBroadcaster();
            var abortRequests = new StrongBox<bool>();

            var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services
                            .AddControllers(o => o.UseRoutePrefix("api"))
                            .AddApplicationPart(typeof(EventVideoController).Assembly);
                        services.AddSingleton(supabase);
                        services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
                        services.AddSingleton<IVideoSfuService>(sfu);
                        services.AddSingleton<IEventVideoBroadcaster>(broadcaster);
                        services.AddSingleton<IAccessTokenVerifier>(new AccessTokenVerifier(TokenSecret));
                        services.Configure<GaleneConfig>(c =>
                        {
                            c.MaxParticipants = 50;
                            c.MaxPresenters = 9;
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        app.Use(async (context, next) =>
                        {
                            if (abortRequests.Value)
                                context.RequestAborted = new CancellationToken(canceled: true);
                            await next(context);
                        });
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .StartAsync();

            return new EventVideoApi(host, sfu, broadcaster, abortRequests);
        }

        public static string Token(Guid userId) =>
            new TokenService(TokenSecret).GenerateTokens(userId.ToString()).AccessToken;

        public Task<HttpResponseMessage> Post(string path, Guid asUser, object? body = null) =>
            Send(HttpMethod.Post, path, Token(asUser), body ?? new { });

        public Task<HttpResponseMessage> Get(string path, Guid asUser) =>
            Send(HttpMethod.Get, path, Token(asUser), null);

        public Task<HttpResponseMessage> PostWithRawToken(string path, string token) =>
            Send(HttpMethod.Post, path, token, new { });

        private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null)
                request.Content = JsonContent.Create(body);
            return Http.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
