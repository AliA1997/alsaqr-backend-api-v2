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
        /// <summary>Live stream for an event, or null when none is live.</summary>
        Task<VideoStreamToDisplay?> GetLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct);

        /// <summary>Live stream for an event; NotFound when none is live.</summary>
        Task<VideoStreamToDisplay> RequireLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Starts the stream with the caller as host. Validation when the event is not
        /// online (C1) or has no host group; Forbidden unless the caller is the event
        /// organizer or group founder; Conflict when a stream is already live.
        /// </summary>
        Task<VideoStream> StartEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>System end (reaper, failed start): no authority check.</summary>
        Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct);

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
    }
}
