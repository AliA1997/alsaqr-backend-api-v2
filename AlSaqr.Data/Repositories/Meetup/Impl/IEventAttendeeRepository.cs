using AlSaqr.Data.Entities.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IEventAttendeeRepository
    {
        Task JoinEvent(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            CancellationToken ct);

        Task LeaveEvent(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            CancellationToken ct);

        /// <summary>
        /// Authorizes an attendee removal and writes nothing: NotFound for an unknown
        /// event, Forbidden unless the caller founded the event's group. Returns the
        /// attendee's live video row with its stream, for the SFU disconnect that must
        /// come before the database removal clears the SFU identity; null when there is
        /// nothing live to cut (in-person event, not live, or no video tables deployed).
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant)?> AuthorizeRemoveEventAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid attendeeUserId,
            CancellationToken ct);

        /// <summary>
        /// Kicks the attendee's live video row (online events only), then removes their
        /// event attendance; they stay in the group. Same NotFound and Forbidden as
        /// AuthorizeRemoveEventAttendee; any other failure is an UnjoinEventException,
        /// and a failed kick removes nothing. Returns true when a live video row was
        /// kicked, so the caller announces participant_left.
        /// </summary>
        Task<bool> RemoveEventAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid attendeeUserId,
            CancellationToken ct);
    }
}
