using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.Meetup.Impl;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class AttendeeRepository : IAttendeeRepository
    {
        public AttendeeRepository() { }

        public async Task<Attendee> InsertOrRetrieveAttendee(
            Supabase.Client client,
            string name,
            Guid userId
        )
        {
            Attendee? attendee = null;
            try
            {
                attendee = (
                    await client
                        .From<Attendee>()
                        .Filter("user_id", Operator.Equals, userId.ToString())
                        .Get()
                ).Model;
                if (attendee == null)
                {
                    //var recentlyInsertedAttendeeId = await client.From<Attendee>().Count(CountType.Estimated);
                    attendee = (
                        await client
                            .From<Attendee>()
                            .Upsert(
                                new Attendee()
                                {
                                    Id = Guid.NewGuid(),
                                    Name = name,
                                    UserId = userId,
                                    CreatedAt = DateTime.UtcNow,
                                },
                                new QueryOptions()
                                {
                                    Returning = QueryOptions.ReturnType.Representation,
                                }
                            )
                    ).Model;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting attendee in repository layer:", ex.Message);
            }

            return attendee!;
        }

        public async Task<Attendee> InsertOrRetrieveAttendeeForUser(
            Supabase.Client client,
            Guid userId,
            CancellationToken ct = default
        )
        {
            var user = await client
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            if (user == null)
                throw new Exception($"User with ID: {userId} not found.");

            // Attendees are named the way GroupsController names the organizer on group creation.
            var name = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrEmpty(name))
                name = user.Username;

            return await InsertOrRetrieveAttendee(client, name, userId);
        }
    }
}
