using AlSaqr.Data.Entities.Meetup;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Tests.Support
{
    [Collection(PostgrestCollection.Name)]
    public class PostgrestHarnessTests : IAsyncLifetime
    {
        private readonly PostgrestFixture _fx;

        public PostgrestHarnessTests(PostgrestFixture fx) => _fx = fx;

        public Task InitializeAsync() => _fx.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SupabaseClient_ReadsSeededEvent_ThroughPostgrest()
        {
            var founder = await _fx.Seed.User("founder");
            var group = await _fx.Seed.Group(founder);
            var eventId = await _fx.Seed.Event(group, isOnline: true);

            var loaded = await _fx.Client
                .From<Event>()
                .Filter("id", Operator.Equals, eventId.ToString())
                .Single();

            Assert.NotNull(loaded);
            Assert.True(loaded!.IsOnline);
            Assert.Equal(group, loaded.GroupId);
        }

        [Fact]
        public async Task EventAttendeesTable_FromRepoDdl_IsQueryable()
        {
            var user = await _fx.Seed.User("attendee");
            var founder = await _fx.Seed.User("founder");
            var group = await _fx.Seed.Group(founder);
            var eventId = await _fx.Seed.Event(group, isOnline: true);
            var attendee = await _fx.Seed.Attendee(user);
            await _fx.Seed.EventAttendee(eventId, attendee, group, isOrganizer: true);

            var row = await _fx.Client
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Single();

            Assert.True(row!.IsEventOrganizer);
        }

        [Fact]
        public async Task Reset_RemovesSeededRows()
        {
            await _fx.Seed.User("leftover");

            await _fx.ResetAsync();

            Assert.Equal(0L, await _fx.Seed.Scalar<long>("select count(*) from \"alsaqr-2026\".users"));
        }
    }
}
