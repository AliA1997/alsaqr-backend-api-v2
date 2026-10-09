namespace AlSaqr.API.HostedServices
{
    /// <summary>
    /// Runs <see cref="IVideoStreamReaper"/> every 30 s and the retention purge once
    /// a day (specs/video-streaming-online-events.md §7, §9). Mirrors
    /// SpaceReaperService, which stays untouched.
    /// </summary>
    public sealed class VideoStreamReaperService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PurgeInterval = TimeSpan.FromDays(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Supabase.Client _supabase;
        private readonly TimeProvider _time;
        private readonly ILogger<VideoStreamReaperService> _logger;
        private DateTime? _lastPurge;

        public VideoStreamReaperService(
            IServiceScopeFactory scopeFactory,
            Supabase.Client supabase,
            TimeProvider time,
            ILogger<VideoStreamReaperService> logger)
        {
            _scopeFactory = scopeFactory;
            _supabase = supabase;
            _time = time;
            _logger = logger;
        }

        // Cancellation propagates: stopping the host cancels the wait and the running sweep.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ScanInterval, _time);

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await TickAsync(_time.GetUtcNow().UtcDateTime, stoppingToken);
        }

        /// <summary>One sweep; a failing sweep is logged and retried on the next tick.</summary>
        public async Task TickAsync(DateTime utcNow, CancellationToken ct)
        {
            try
            {
                // Scoped repository + typed HttpClients: resolve per tick so handler
                // rotation keeps working instead of pinning one handler here.
                using var scope = _scopeFactory.CreateScope();
                var reaper = scope.ServiceProvider.GetRequiredService<IVideoStreamReaper>();

                await reaper.ReapOnceAsync(_supabase, utcNow, ct);

                if (_lastPurge is null || utcNow - _lastPurge >= PurgeInterval)
                {
                    var purged = await reaper.PurgeAsync(_supabase, utcNow, ct);
                    _lastPurge = utcNow;
                    _logger.LogInformation("Purged {Count} video participation rows past retention.", purged);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video stream reaper scan failed; retrying next tick.");
            }
        }
    }
}
