using AlSaqr.Data.Entities.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IGroupAttendeeRepository
    {
        Task JoinGroup(
            Supabase.Client supabase,
            Guid userId,
            Guid groupId,
            CancellationToken ct);

        Task RemoveGroupAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid groupId,
            Guid attendeeUserId,
            CancellationToken ct);

        /// <summary>
        /// Returns the group_attendees row linking <paramref name="attendee"/> to the group,
        /// creating it when the attendee is not on the group yet.
        /// </summary>
        Task<GroupAttendees> InsertOrRetrieveGroupAttendee(
            Supabase.Client supabase,
            Guid groupId,
            Attendee attendee,
            bool isGroupOrganizer = false,
            CancellationToken ct = default);

        Task InsertGroupAttendees(
            Supabase.Client supabase,
            Guid groupId,
            List<IDictionary<string, object>> groupAttendees,
            CancellationToken ct = default);
    }
}
