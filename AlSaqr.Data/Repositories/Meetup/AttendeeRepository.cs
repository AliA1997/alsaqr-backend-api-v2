
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class AttendeeRepository: IAttendeeRepository
    {
        public AttendeeRepository() { }

        public async Task<Attendee> InsertOrRetrieveAttendee(Supabase.Client client, string name, string neo4jUserId)
        {
            Attendee? attendee = null;
            try
            {
                attendee = (await client.From<Attendee>().Filter("neo4j_user_id", Operator.Equals, neo4jUserId).Get()).Model;
                if (attendee == null)
                {
                    var recentlyInsertedAttendeeId = await client.From<Attendee>().Count(CountType.Estimated);
                    attendee = (
                        await client.From<Attendee>().Upsert(new Attendee()
                        {
                            Id = recentlyInsertedAttendeeId + 1,
                            Name = name,
                            Neo4jUserId = neo4jUserId,
                            CreatedAt = DateTime.UtcNow
                        }, new QueryOptions() { Returning = QueryOptions.ReturnType.Representation })).Model;

                }
            } catch(Exception ex)
            {
                Console.WriteLine("Error getting attendee in repository layer:", ex.Message);
            }

            return attendee!;

        }

        public async Task InsertGroupAttendees(Supabase.Client client, int groupId, List<IDictionary<string, object>> groupAttendees)
        {
            foreach(var groupAttendee in groupAttendees)
            {
                var attendee = await InsertOrRetrieveAttendee(client, groupAttendee["name"].ToString(), groupAttendee["id"].ToString());
                var recentInsertedGroupAttendee = await client.From<GroupAttendees>().Count(CountType.Estimated);

                await client.From<GroupAttendees>().Upsert(
                    new GroupAttendees()
                    {
                        Id = recentInsertedGroupAttendee + 1,
                        GroupId = groupId,
                        AttendeeId = attendee.Id,
                        IsGroupOrganizer = false,
                        CreatedAt = DateTime.UtcNow
                    }
                );
            }
        }
    }
}
