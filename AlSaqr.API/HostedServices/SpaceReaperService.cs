using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Infrastructure.Spaces;

namespace AlSaqr.API.HostedServices
{
    /// <summary>
    /// Reaps silently-dead space participants (specs/audio-spaces.md). A client
    /// that dies without calling leave (network death, tab close) stops refreshing
    /// last_seen_at; once it exceeds the timeout this service closes their SFU
    /// track so Cloudflare stops pulling audio nobody hears, and a live space left
    /// empty past the timeout is ended. Every teardown converges on the same
    /// repository + SFU + broadcast steps the explicit leave/end paths use.
    /// </summary>
    public sealed class SpaceReaperService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(60);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Supabase.Client _supabase;
        private readonly ILogger<SpaceReaperService> _logger;

        public SpaceReaperService(
            IServiceScopeFactory scopeFactory,
            Supabase.Client supabase,
            ILogger<SpaceReaperService> logger)
        {
            _scopeFactory = scopeFactory;
            _supabase = supabase;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ScanInterval);

            while (await WaitForNextTick(timer, stoppingToken))
            {
                try
                {
                    await ReapOnce(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // The reaper must survive transient Supabase/Cloudflare failures;
                    // it will retry on the next tick.
                    _logger.LogError(ex, "Space reaper scan failed; retrying next tick.");
                }
            }
        }

        private async Task ReapOnce(CancellationToken ct)
        {
            // ISpaceRepository is scoped and the SFU/broadcast services are typed
            // HttpClients (transient) — resolving them per scan keeps handler
            // rotation working instead of pinning one handler in this singleton.
            using var scope = _scopeFactory.CreateScope();
            var spaces = scope.ServiceProvider.GetRequiredService<ISpaceRepository>();
            var cloudflareCalls = scope.ServiceProvider.GetRequiredService<ICloudflareCallsService>();
            var broadcaster = scope.ServiceProvider.GetRequiredService<ISpaceEventBroadcaster>();

            var cutoff = DateTime.UtcNow - ParticipantTimeout;

            foreach (var space in await spaces.GetLiveSpaces(_supabase, ct))
            {
                var live = await spaces.GetLiveParticipants(_supabase, space.Id, ct);

                foreach (var participant in live.Where(p => p.LastSeenAt < cutoff))
                {
                    var sfuSessionId = participant.SfuSessionId;
                    var sfuMid = participant.SfuMid;

                    await spaces.MarkParticipantLeft(_supabase, participant, ct);

                    if (!string.IsNullOrEmpty(sfuSessionId) && !string.IsNullOrEmpty(sfuMid))
                        await cloudflareCalls.CloseTrackAsync(sfuSessionId, sfuMid, ct);

                    await broadcaster.TrackClosedAsync(space.Id, participant.UserId, ct);

                    _logger.LogInformation(
                        "Reaped participant {UserId} from space {SpaceId} (last seen {LastSeenAt:O}).",
                        participant.UserId, space.Id, participant.LastSeenAt);
                }

                // End a space nobody is (responsively) in anymore. Using the same
                // cutoff means a space only dies after every participant has been
                // silent past the timeout — including the host.
                var remaining = await spaces.GetLiveParticipants(_supabase, space.Id, ct);
                if (remaining.Count == 0)
                {
                    var ended = await spaces.EndSpaceSystem(_supabase, space, ct);
                    await broadcaster.SpaceEndedAsync(ended.Id, ended.EndedAt!.Value, ct);

                    _logger.LogInformation("Reaper ended empty space {SpaceId}.", ended.Id);
                }
            }
        }

        private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken ct)
        {
            try
            {
                return await timer.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
