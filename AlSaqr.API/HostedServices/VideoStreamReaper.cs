using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure.Video;

namespace AlSaqr.API.HostedServices
{
    /// <summary>
    /// The teardown guarantees of specs/video-streaming-online-events.md §7 plus the §9
    /// retention purge. Every teardown converges on the same repository, SFU and broadcast
    /// steps the explicit leave/end paths use. Takes utcNow explicitly so tests control
    /// time; the time windows themselves are evaluated by the repository queries.
    /// Failures are isolated per participant and per stream: one bad item never aborts
    /// the sweep, and the next sweep retries whatever is still live.
    /// </summary>
    public interface IVideoStreamReaper
    {
        Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct);

        /// <summary>Retention purge. Called only from the hosted service, never from an endpoint.</summary>
        Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct);
    }

    public sealed class VideoStreamReaper : IVideoStreamReaper
    {
        public static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(60);   // C9
        public static readonly TimeSpan MaxStreamDuration = TimeSpan.FromHours(4);       // C11

        /// <summary>
        /// The SFU room's own access cutoff sits this far past MaxStreamDuration, so
        /// the reaper ends the stream first and the cutoff is only a backstop.
        /// </summary>
        public static readonly TimeSpan RoomGrace = TimeSpan.FromMinutes(5);

        public static readonly TimeSpan Retention = TimeSpan.FromDays(90);              // §9

        private readonly IVideoStreamRepository _streams;
        private readonly IVideoSfuService _sfu;
        private readonly IEventVideoBroadcaster _broadcaster;
        private readonly ILogger<VideoStreamReaper> _logger;

        public VideoStreamReaper(
            IVideoStreamRepository streams,
            IVideoSfuService sfu,
            IEventVideoBroadcaster broadcaster,
            ILogger<VideoStreamReaper> logger)
        {
            _streams = streams;
            _sfu = sfu;
            _broadcaster = broadcaster;
            _logger = logger;
        }

        public async Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct)
        {
            var expired = (await _streams.GetLiveStreamsStartedBefore(supabase, utcNow - MaxStreamDuration, ct))
                .Select(s => s.Id)
                .ToHashSet();

            foreach (var stream in await _streams.GetLiveStreams(supabase, ct))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await ReapStream(supabase, stream, expired.Contains(stream.Id), utcNow, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reaping video stream {StreamId} failed; continuing with the next stream.", stream.Id);
                }
            }
        }

        private async Task ReapStream(
            Supabase.Client supabase, VideoStream stream, bool isExpired, DateTime utcNow, CancellationToken ct)
        {
            var reason = isExpired ? VideoParticipant.LeftExpired : VideoParticipant.LeftReaped;

            // C11 removes everyone; otherwise only participants silent past the timeout (C9).
            var doomed = isExpired
                ? await _streams.GetLiveParticipants(supabase, stream.Id, ct)
                : await _streams.GetStaleLiveParticipants(supabase, stream.Id, utcNow - ParticipantTimeout, ct);

            var identities = new List<string>();
            foreach (var participant in doomed)
            {
                var identity = participant.SfuIdentity;
                try
                {
                    await _streams.MarkParticipantLeft(supabase, participant, reason, ct);
                    if (!string.IsNullOrEmpty(identity))
                        identities.Add(identity);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not remove participant {UserId} from video stream {StreamId}; retrying next sweep.",
                        participant.ParticipantId, stream.Id);
                    continue;
                }

                await TryBroadcastLeft(stream, participant.ParticipantId, reason, ct);
            }

            var live = await _streams.GetLiveParticipants(supabase, stream.Id, ct);
            var hostLive = live.Any(p => p.ParticipantId == stream.HostId);

            // A stream with nobody left, or whose host is gone, cannot be steered; end it.
            if (live.Count > 0 && hostLive)
            {
                // One operator session per room per sweep, not one per participant.
                if (identities.Count > 0)
                    await TryRemoveFromRoom(stream.RoomName, identities, ct);
                return;
            }

            var ended = await _streams.EndStreamSystem(supabase, stream, ct);

            // Deleting the room disconnects everyone, so no separate kicks are needed.
            try
            {
                await _sfu.DeleteRoomAsync(ended.RoomName, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deleting SFU room {Room} failed; the room's access cutoff is the backstop.", ended.RoomName);
            }

            try
            {
                await _broadcaster.StreamEndedAsync(ended.EventId, ended.Id, ended.EndedAt ?? utcNow, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcasting end of video stream {StreamId} failed.", ended.Id);
            }

            _logger.LogInformation("Reaper ended video stream {StreamId} ({Why}).", ended.Id,
                isExpired ? "4-hour limit" : live.Count == 0 ? "empty" : "host gone");
        }

        private async Task TryRemoveFromRoom(string room, IReadOnlyCollection<string> identities, CancellationToken ct)
        {
            try
            {
                await _sfu.RemoveParticipantsAsync(room, identities, ct);
            }
            catch (ParticipantNotConnectedException)
            {
                // Already gone from the SFU: exactly the state we want.
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Removing participants from SFU room {Room} failed.", room);
            }
        }

        private async Task TryBroadcastLeft(VideoStream stream, Guid participantId, string reason, CancellationToken ct)
        {
            try
            {
                await _broadcaster.ParticipantLeftAsync(stream.EventId, participantId, reason, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcasting participant {UserId} leaving video stream {StreamId} failed.",
                    participantId, stream.Id);
            }
        }

        public Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct) =>
            _streams.PurgeParticipantsLeftBefore(supabase, utcNow - Retention, ct);
    }
}
