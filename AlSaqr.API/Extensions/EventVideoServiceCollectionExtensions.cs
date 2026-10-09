using AlSaqr.API.HostedServices;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AlSaqr.API.Extensions
{
    public static class EventVideoServiceCollectionExtensions
    {
        // The reaper calls both per stream on every 30 s sweep, so a hung peer must
        // cost seconds, not HttpClient's 100 s default.
        private static readonly TimeSpan SfuAdminTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan BroadcastTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Online-event video streaming: repository, self-hosted Galène control plane,
        /// realtime broadcaster, and the reaper/purge host. Requires
        /// IAccessTokenVerifier, Supabase.Client and SupabaseRealtimeConfig to be
        /// registered already. A missing or weak Galène secret fails at startup, not
        /// on the first call.
        /// </summary>
        public static IServiceCollection AddEventVideo(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<GaleneConfig>()
                .Bind(configuration.GetSection("Galene"))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<GaleneConfig>, GaleneConfigValidator>();

            services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
            services.AddSingleton<IGaleneOperator, GaleneOperator>();
            // Both clients carry a server-side credential (Galène admin password, Supabase
            // service-role key): never over cleartext to a remote host, and never re-sent
            // to wherever a redirect points.
            services.AddHttpClient<IVideoSfuService, GaleneVideoService>(c => c.Timeout = SfuAdminTimeout)
                .ConfigurePrimaryHttpMessageHandler(NoRedirects)
                .AddHttpMessageHandler(() => new HttpsOrLoopbackHandler());
            services.AddHttpClient<ISupabaseBroadcastClient, SupabaseBroadcastClient>(c => c.Timeout = BroadcastTimeout)
                .ConfigurePrimaryHttpMessageHandler(NoRedirects)
                .AddHttpMessageHandler(() => new HttpsOrLoopbackHandler());
            services.AddScoped<IEventVideoBroadcaster, EventVideoBroadcaster>();

            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped<IVideoStreamReaper, VideoStreamReaper>();
            services.AddHostedService<VideoStreamReaperService>();
            return services;
        }

        private static HttpMessageHandler NoRedirects() => new HttpClientHandler { AllowAutoRedirect = false };
    }
}
