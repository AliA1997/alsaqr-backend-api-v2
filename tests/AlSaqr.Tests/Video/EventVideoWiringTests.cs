using System.Net;
using System.Text.RegularExpressions;
using AlSaqr.API.Controllers.Meetup;
using AlSaqr.API.Extensions;
using AlSaqr.API.HostedServices;
using AlSaqr.Data;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AlSaqr.Tests.Video
{
    public class EventVideoWiringTests
    {
        private static ServiceProvider Build(Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new Supabase.Client("http://supabase.test", null));
            services.AddSingleton<IAccessTokenVerifier>(new AccessTokenVerifier(EventVideoApi.TokenSecret));
            services.Configure<SupabaseRealtimeConfig>(configuration.GetSection("Supabase"));   // Program.cs already does this

            services.AddEventVideo(configuration);
            configure?.Invoke(services);

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }

        private static Dictionary<string, string?> ValidSettings() => new()
        {
            ["Galene:AdminUrl"] = "https://galene.internal:8443",
            ["Galene:PublicUrl"] = "https://video.test",
            ["Galene:AdminUsername"] = "alsaqr-admin",
            ["Galene:AdminPassword"] = "admin-password",
            ["Galene:TokenSecret"] = "test-galene-token-secret-0123456",   // Galène needs exactly 32 bytes
            ["Supabase:Url"] = "https://supabase.test",
            ["Supabase:ServiceRoleSecret"] = "s",
        };

        [Fact]
        public void AddEventVideo_BuildsAValidGraph_ForControllerAndReaper()
        {
            using var provider = Build(ValidSettings());
            using var scope = provider.CreateScope();

            Assert.NotNull(ActivatorUtilities.CreateInstance<EventVideoController>(scope.ServiceProvider));
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IVideoStreamReaper>());
            Assert.Contains(provider.GetServices<IHostedService>(), s => s is VideoStreamReaperService);
        }

        [Theory]
        [InlineData("Galene:TokenSecret", "too-short")]
        [InlineData("Galene:AdminPassword", "")]
        [InlineData("Galene:PublicUrl", "")]
        [InlineData("Galene:AdminUrl", "http://galene.internal:8443")]
        public void InvalidGaleneConfig_FailsAtStartup(string key, string value)
        {
            var settings = ValidSettings();
            settings[key] = value;
            using var provider = Build(settings);

            // The host runs IStartupValidator at boot (ValidateOnStart), so a bad config
            // stops the app before the first request rather than failing on it.
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GaleneConfig>>().Value);
        }

        [Fact]
        public void OutboundHttpClients_HaveShortTimeouts()
        {
            // The reaper calls both per stream on every sweep: a hung peer must not stall it for the 100 s default.
            using var provider = Build(ValidSettings());
            var factory = provider.GetRequiredService<IHttpClientFactory>();

            Assert.Equal(TimeSpan.FromSeconds(5), factory.CreateClient(nameof(ISupabaseBroadcastClient)).Timeout);
            Assert.Equal(TimeSpan.FromSeconds(10), factory.CreateClient(nameof(IVideoSfuService)).Timeout);
        }

        [Theory]
        [InlineData(nameof(IVideoSfuService))]
        [InlineData(nameof(ISupabaseBroadcastClient))]
        public void CredentialCarryingClients_RequireHttpsOrLoopback_AndNeverFollowRedirects(string clientName)
        {
            using var provider = Build(ValidSettings());
            var handlers = new List<HttpMessageHandler>();
            for (HttpMessageHandler? h = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
                 h != null;
                 h = (h as DelegatingHandler)?.InnerHandler)
            {
                handlers.Add(h);
            }

            Assert.Contains(handlers, h => h is HttpsOrLoopbackHandler);
            var primary = Assert.IsType<HttpClientHandler>(handlers[^1]);
            Assert.False(primary.AllowAutoRedirect);
        }

        [Theory]
        [InlineData("http://supabase.test", false)]
        [InlineData("https://supabase.test", true)]
        [InlineData("http://127.0.0.1:54321", true)]
        [InlineData("http://localhost:54321", true)]
        public async Task SupabaseBroadcast_SendsServiceRoleKeyOnlyOverHttpsOrLoopback(string supabaseUrl, bool sent)
        {
            var settings = ValidSettings();
            settings["Supabase:Url"] = supabaseUrl;
            var upstream = new RecordingHandler();
            using var provider = Build(settings, services => services
                .AddHttpClient(nameof(ISupabaseBroadcastClient))
                .ConfigurePrimaryHttpMessageHandler(() => upstream));

            var broadcast = provider.GetRequiredService<ISupabaseBroadcastClient>()
                .BroadcastAsync("event_video:test", "stream_started", new { });

            if (sent)
            {
                await broadcast;
                Assert.Single(upstream.Requests);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => broadcast);
                Assert.Empty(upstream.Requests);
            }
        }

        [Fact]
        public async Task Program_ResolvesEventVideoControllerAndReaper()
        {
            // The real Program.cs registrations (Development: ValidateOnBuild + ValidateScopes,
            // Galène settings from appsettings.Development.json, ValidateOnStart run at boot).
            await using var factory = new ProgramFactory();
            using var scope = factory.Services.CreateScope();

            Assert.NotNull(ActivatorUtilities.CreateInstance<EventVideoController>(scope.ServiceProvider));
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IVideoStreamReaper>());

            // Program registers the reaper host (the factory removed it so it never sweeps
            // during the test); its dependencies still resolve from the real container.
            Assert.True(factory.ReaperHostWasRegistered);
            Assert.NotNull(ActivatorUtilities.CreateInstance<VideoStreamReaperService>(factory.Services));

            // MVC activates the controller from the same container; with no token the action refuses the call.
            using var http = factory.CreateClient();
            using var response = await http.GetAsync($"/api/EventVideo/event/{Guid.NewGuid()}/live");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void NoRecordingPermissionOrActionExistsAnywhere()
        {
            // C10: no token may carry "record", no group action may start recording,
            // and no group may be created with recording allowed.
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "AlSaqr.API.sln")))
                root = root.Parent!;

            // Production sources only: skip build output, tests, and hidden folders
            // (.git, and .claude worktrees, which hold other checkouts).
            var sources = Directory
                .EnumerateFiles(root.FullName, "*.cs", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(root.FullName, f)
                    .Split(Path.DirectorySeparatorChar)[..^1]
                    .Any(d => d is "bin" or "obj" or "tests" || d.StartsWith('.')))
                .ToList();

            Assert.Contains(sources, f => f.EndsWith($"{Path.DirectorySeparatorChar}GaleneVideoService.cs"));

            var offenders = sources
                .Where(f => Regex.IsMatch(
                    File.ReadAllText(f), "\"(record|unrecord)\"|AllowRecording\\s*=\\s*true", RegexOptions.IgnoreCase))
                .ToList();

            Assert.Empty(offenders);
        }

        /// <summary>
        /// Boots Program.cs without its external side effects: no AWS Secrets Manager
        /// read, no Supabase connection at startup, and no space or video reaper sweeping.
        /// </summary>
        private sealed class ProgramFactory : WebApplicationFactory<Program>
        {
            public bool ReaperHostWasRegistered { get; private set; }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("SecretsKey", " ");
                builder.UseSetting("Supabase:Url", "http://localhost:54321");
                builder.UseSetting("Supabase:Key", "test-anon-key");
                builder.UseSetting("Supabase:ServiceRoleSecret", "test-service-role-key");
                builder.UseSetting("JwtSettings:Key", EventVideoApi.TokenSecret);
                builder.UseSetting("JwtSettings:Secret", EventVideoApi.TokenSecret);

                builder.ConfigureTestServices(services =>
                {
                    ReaperHostWasRegistered = services.Any(d => d.ServiceType == typeof(IHostedService)
                                                             && d.ImplementationType == typeof(VideoStreamReaperService));

                    var external = services
                        .Where(d => d.ServiceType == typeof(IHostedService)
                                 && (d.ImplementationType == typeof(SupabaseInitializer)
                                     || d.ImplementationType == typeof(SpaceReaperService)
                                     || d.ImplementationType == typeof(VideoStreamReaperService)))
                        .ToList();
                    foreach (var descriptor in external)
                        services.Remove(descriptor);
                });
            }
        }
    }
}
