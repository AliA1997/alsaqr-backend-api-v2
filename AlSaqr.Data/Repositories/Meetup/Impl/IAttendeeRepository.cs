using AlSaqr.Data.Entities.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IAttendeeRepository
    {
        Task<Attendee> InsertOrRetrieveAttendee(Supabase.Client client, string name, Guid userId);

        /// <summary>
        /// Resolves the attendees row for a user, creating it on first use. Shared by the group
        /// and event attendee repositories, which both key their rows on attendee_id.
        /// </summary>
        Task<Attendee> InsertOrRetrieveAttendeeForUser(
            Supabase.Client client,
            Guid userId,
            CancellationToken ct = default);
    }
}
