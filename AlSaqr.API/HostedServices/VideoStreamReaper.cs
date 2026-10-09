using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Common;
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

        /// <summary>
        /// SFU first, database second: DB state only advances after the SFU action
        /// succeeded, so a failure leaves the row/stream live and the next sweep retries
        /// it. Consequence: a prolonged SFU outage keeps dead participants and expired
        /// streams live in the database until the SFU recovers.
        /// </summary>
        private async Task ReapStream(
            Supabase.Client supabase, VideoStream stream, bool isExpired, DateTime utcNow, CancellationToken ct)
        {
            var reason = isExpired ? VideoParticipant.LeftExpired : VideoParticipant.LeftReaped;
            var live = await _streams.GetLiveParticipants(supabase, stream.Id, ct);

            // C11 removes everyone; otherwise only participants silent past the timeout (C9).
            var staleCutoff = utcNow - ParticipantTimeout;
            var doomed = isExpired
                ? live
                : await _streams.GetStaleLiveParticipants(supabase, stream.Id, staleCutoff, ct);
            var doomedIds = doomed.Select(p => p.Id).ToHashSet();
            var remaining = live.Where(p => !doomedIds.Contains(p.Id)).ToList();

            var shouldEnd = isExpired || remaining.Count == 0 || !remaining.Any(p => p.ParticipantId == stream.HostId);

            // Fresh streams get the same grace as a heartbeat: the host row is inserted
            // just after the stream row, so "no host yet" is not "host gone".
            if (shouldEnd && !isExpired && stream.StartedAt.ToUniversalTime() > staleCutoff.ToUniversalTime())
                return;

            if (shouldEnd)
            {
                await EndStream(supabase, stream, doomed, reason, isExpired, utcNow, ct);
                return;
            }

            if (doomed.Count == 0)
                return;

            var identities = doomed.Where(p => !string.IsNullOrEmpty(p.SfuIdentity)).Select(p => p.SfuIdentity!).ToList();

            // One operator session per room per sweep, not one per participant.
            if (identities.Count > 0 && !await TryRemoveFromRoom(stream.RoomName, identities, ct))
                return;

            foreach (var participant in doomed)
            {
                try
                {
                    await _streams.MarkParticipantLeft(supabase, participant, reason, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not mark participant {UserId} left on video stream {StreamId}; retrying next sweep.",
                        participant.ParticipantId, stream.Id);
                    continue;
                }

                await TryBroadcastLeft(stream, participant.ParticipantId, reason, ct);
            }
        }

        private async Task EndStream(
            Supabase.Client supabase, VideoStream stream, List<VideoParticipant> doomed, string reason,
            bool isExpired, DateTime utcNow, CancellationToken ct)
        {
            // Deleting the room disconnects everyone, so no separate kicks are needed.
            // If it fails the stream stays live and the next sweep retries.
            try
            {
                await _sfu.DeleteRoomAsync(stream.RoomName, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deleting SFU room for video stream {StreamId} failed; stream stays live, retrying next sweep.", stream.Id);
                return;
            }

            foreach (var participant in doomed)
            {
                try
                {
                    await _streams.MarkParticipantLeft(supabase, participant, reason, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // EndStreamSystem marks any row still live as ended, so the stream ends regardless.
                    _logger.LogWarning(ex, "Could not mark participant {UserId} left on video stream {StreamId}.",
                        participant.ParticipantId, stream.Id);
                }
            }

            VideoStream ended;
            try
            {
                ended = await _streams.EndStreamSystem(supabase, stream, ct);
            }
            catch (ConflictException)
            {
                _logger.LogInformation("Video stream {StreamId} was already ended by someone else.", stream.Id);
                return;
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
                isExpired ? "4-hour limit" : "empty or host gone");
        }

        /// <summary>True when the identities are gone from the SFU (already gone counts).</summary>
        private async Task<bool> TryRemoveFromRoom(string room, IReadOnlyCollection<string> identities, CancellationToken ct)
        {
            try
            {
                await _sfu.RemoveParticipantsAsync(room, identities, ct);
                return true;
            }
            catch (ParticipantNotConnectedException)
            {
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Removing participants from SFU room failed; they stay live, retrying next sweep.");
                return false;
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
