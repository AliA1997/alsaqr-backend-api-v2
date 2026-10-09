using AlSaqr.Data.Entities.Meetup;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    /// <summary>
    /// Online-event video streams (specs/video-streaming-online-events.md §6). Every
    /// authorization rule of the feature lives here; methods are keyed on eventId
    /// because exactly one stream per event can be live.
    /// </summary>
    public interface IVideoStreamRepository
    {
        /// <summary>Live stream for an event, or null when none is live. No authorization check.</summary>
        Task<VideoStreamToDisplay?> GetLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Live stream for an event as seen by the user, or null when none is live.
        /// NotFound for an unknown event, Validation when it is not online, Forbidden
        /// unless the user attends the event or may control its video (same gate as join).
        /// </summary>
        Task<VideoStreamToDisplay?> GetLiveEventStreamForUser(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>Live stream for an event; NotFound when none is live.</summary>
        Task<VideoStreamToDisplay> RequireLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Starts the stream with the caller as host. Validation when the event is not
        /// online (C1) or has no host group; Forbidden unless the caller is the event
        /// organizer or group founder; Conflict when a stream is already live.
        /// </summary>
        Task<VideoStream> StartEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Ends exactly this stream and marks every live participant 'ended'. No authority
        /// check: callers are the reaper, a failed start, or an end already authorized by
        /// RequireStreamControl. Conflict when the stream had already ended.
        /// </summary>
        Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct);

        /// <summary>
        /// The live stream, once the caller is confirmed as its host, the event organizer,
        /// or the group founder. Validation when the event is not online, NotFound when no
        /// stream is live, Forbidden otherwise. Writes nothing, so the controller can act
        /// on the SFU before any database change.
        /// </summary>
        Task<VideoStream> RequireStreamControl(Supabase.Client supabase, Guid callerId, Guid eventId, CancellationToken ct);

        /// <summary>The user's live participant row with its stream, or null when not live. Writes nothing.</summary>
        Task<(VideoStream Stream, VideoParticipant Participant)?> FindLiveParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>Participants with left_at null, ordered joined_at asc, id asc.</summary>
        Task<List<VideoParticipant>> GetLiveParticipants(Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

        /// <summary>Stamps left_at + left_reason and clears the SFU identity.</summary>
        Task MarkParticipantLeft(Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct);

        /// <summary>
        /// Registers the caller on the live stream. NotFound when no stream is live;
        /// Forbidden unless the caller attends the event or may control its video.
        /// Re-checked on every join. Honours the 5-minute rejoin grace.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant, bool HadLiveConnection)> JoinEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Marks the caller left with the reason. Returns the stream and the pre-leave
        /// snapshot (for the SFU disconnect), or null when the caller was not live.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Snapshot)?> LeaveEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, string reason, CancellationToken ct);

        /// <summary>
        /// Refreshes last_seen_at and returns the caller's live row with its stream.
        /// NotFound for no live stream, Forbidden when not live on it.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant)> TouchLiveParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>Persists camera/mute state (a cost signal, not a permission).</summary>
        Task SetCameraState(
            Supabase.Client supabase, Guid userId, Guid eventId, bool cameraEnabled, bool muted, CancellationToken ct);

        /// <summary>Live roster, ordered joined_at asc, id asc.</summary>
        Task<List<VideoParticipantDto>> GetParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

        /// <summary>
        /// Presenter control. Viewer to presenter: Forbidden unless the caller is the host,
        /// event organizer, or group founder; NotFound when the target is not live;
        /// Conflict when host + presenters already equals maxPresenters (C5). Idempotent
        /// for an existing presenter or host.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant)> PromotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, int maxPresenters, CancellationToken ct);

        /// <summary>Presenter to viewer. Same authority as promote; Validation for the host.</summary>
        Task<(VideoStream Stream, VideoParticipant Participant)> DemotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct);

        /// <summary>
        /// Removal from the event: marks the user's live row left with reason 'kicked',
        /// resets the role to viewer and clears the SFU identity, in one guarded update.
        /// Returns the stream and the updated row, or null when the user was not live.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant)?> KickParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        // ----- Reaper support (spec 7) -----

        Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct);

        /// <summary>Live streams whose started_at is before the cutoff (C11 expiry).</summary>
        Task<List<VideoStream>> GetLiveStreamsStartedBefore(
            Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct);

        /// <summary>Live participants whose last_seen_at is before the cutoff (C9).</summary>
        Task<List<VideoParticipant>> GetStaleLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, DateTime cutoffUtc, CancellationToken ct);

        // ----- Retention (spec 9) -----

        /// <summary>
        /// Deletes participant rows with left_at before the cutoff, then streams that
        /// ended before it. Returns the number of participant rows deleted.
        /// </summary>
        Task<int> PurgeParticipantsLeftBefore(Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct);
    }
}
