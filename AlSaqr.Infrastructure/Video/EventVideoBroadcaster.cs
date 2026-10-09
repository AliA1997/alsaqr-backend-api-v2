using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// Publishes the backend-authoritative events of
    /// specs/video-streaming-online-events.md on the Supabase realtime channel
    /// <c>event_video:{eventId}</c> using the service-role key. Camera/mic toggles
    /// are client-emitted through LiveKit's own track events and are NOT re-emitted
    /// here. Presence is Supabase, media is LiveKit — this service only ever touches
    /// the Supabase side.
    /// </summary>
    public interface IEventVideoBroadcaster
    {
        /// <summary>After a stream is started, so attendees can surface "live now".</summary>
        Task StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, CancellationToken ct = default);

        /// <summary>After host end, C11 expiry, or the reaper ending an empty stream.</summary>
        Task StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, CancellationToken ct = default);

        /// <summary>
        /// After a successful join — the "he's in the live" signal. Carries the full
        /// roster entry so other clients add it without refetching the roster.
        /// </summary>
        Task ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, CancellationToken ct = default);

        /// <summary>After leave / removal / reap.</summary>
        Task ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, CancellationToken ct = default);

        /// <summary>After promote / demote; the client re-joins to obtain a new token.</summary>
        Task RoleChangedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default);
    }

    public sealed class EventVideoBroadcaster : IEventVideoBroadcaster
    {
        private readonly ISupabaseBroadcastClient _broadcast;

        public EventVideoBroadcaster(ISupabaseBroadcastClient broadcast) => _broadcast = broadcast;

        private static string Topic(Guid eventId) => $"event_video:{eventId}";

        public Task StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "stream_started", new { videoStreamId, eventId, startedAt }, ct);

        public Task StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "stream_ended", new { videoStreamId, endedAt }, ct);

        public Task ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "participant_joined", participant, ct);

        public Task ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "participant_left", new { participantId, reason }, ct);

        public Task RoleChangedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "role_changed", new { participantId, role }, ct);
    }
}
