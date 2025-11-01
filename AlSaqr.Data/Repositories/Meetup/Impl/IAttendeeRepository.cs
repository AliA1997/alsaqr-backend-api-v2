using AlSaqr.Data.Entities.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IAttendeeRepository
    {
        Task<Attendee> InsertOrRetrieveAttendee(Supabase.Client client, string name, string neo4jUserId);
        Task InsertGroupAttendees(Supabase.Client client, int groupId, List<IDictionary<string, object>> groupAttendees);
    }
}
