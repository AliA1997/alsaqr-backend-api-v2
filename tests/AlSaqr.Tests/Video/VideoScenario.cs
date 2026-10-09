using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// One online event in a group, with a founder (not an event attendee), an
    /// organizer, a plain attendee, and an outsider who attends nothing.
    /// </summary>
    public sealed record VideoScenario(
        Guid EventId, Guid GroupId, Guid FounderId, Guid OrganizerId, Guid AttendeeId, Guid OutsiderId)
    {
        public static async Task<VideoScenario> Create(Seed seed, bool isOnline = true)
        {
            var founder = await seed.User("founder");
            var organizer = await seed.User("organizer");
            var attendee = await seed.User("attendee");
            var outsider = await seed.User("outsider");

            var group = await seed.Group(founder);
            var eventId = await seed.Event(group, isOnline);

            await seed.EventAttendee(eventId, await seed.Attendee(organizer), group, isOrganizer: true);
            await seed.EventAttendee(eventId, await seed.Attendee(attendee), group);
            await seed.Attendee(outsider);   // has an attendee row, attends no event

            return new VideoScenario(eventId, group, founder, organizer, attendee, outsider);
        }

        public async Task<Guid> AddAttendee(Seed seed, string username)
        {
            var user = await seed.User(username);
            await seed.EventAttendee(EventId, await seed.Attendee(user), GroupId);
            return user;
        }
    }
}
