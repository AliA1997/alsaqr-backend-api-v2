using AlSaqr.Data.Entities.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IAttendeeRepository
    {
        Task<Attendee> InsertOrRetrieveAttendee(Supabase.Client client, string name, Guid userId);
        Task InsertGroupAttendees(
            Supabase.Client client,
            Guid groupId,
            List<IDictionary<string, object>> groupAttendees
        );
    }
}
