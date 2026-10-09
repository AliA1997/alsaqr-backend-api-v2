using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Tests.Support;
using Npgsql;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoSchemaTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;

        public VideoSchemaTests(PostgrestFixture fx) => _fx = fx;

        public Task InitializeAsync() => _fx.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<(Guid EventId, Guid HostId)> OnlineEvent()
        {
            var host = await _fx.Seed.User("host");
            var group = await _fx.Seed.Group(host);
            return (await _fx.Seed.Event(group, isOnline: true), host);
        }

        private Task<Guid> InsertStream(Guid eventId, Guid hostId, bool ended) =>
            _fx.Seed.Scalar<Guid>(
                $"insert into {S}.video_streams (event_id, room_name, host_id, ended_at) " +
                "values (@event, @room, @host, case when @ended then now() else null end) returning id",
                ("event", eventId), ("room", $"stream-{Guid.NewGuid()}"), ("host", hostId), ("ended", ended));

        [Fact]
        public async Task SecondLiveStreamForSameEvent_IsRejectedByDatabase()
        {
            var (eventId, host) = await OnlineEvent();
            await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertStream(eventId, host, ended: false));
            Assert.Equal("23505", ex.SqlState);
        }

        [Fact]
        public async Task EndedStreams_DoNotBlockANewLiveStream()
        {
            var (eventId, host) = await OnlineEvent();
            await InsertStream(eventId, host, ended: true);

            await InsertStream(eventId, host, ended: false);   // must not throw
        }

        [Fact]
        public async Task LeftAtWithoutLeftReason_IsRejected()
        {
            var (eventId, host) = await OnlineEvent();
            var stream = await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"insert into {S}.video_participants (event_id, participant_id, video_stream_id, left_at) " +
                "values (@event, @user, @stream, now())",
                ("event", eventId), ("user", host), ("stream", stream)));
            Assert.Equal("23514", ex.SqlState);
        }

        [Fact]
        public async Task DemotedIsNotAValidLeftReason()
        {
            var (eventId, host) = await OnlineEvent();
            var stream = await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"insert into {S}.video_participants (event_id, participant_id, video_stream_id, left_at, left_reason) " +
                "values (@event, @user, @stream, now(), 'demoted')",
                ("event", eventId), ("user", host), ("stream", stream)));
            Assert.Equal("23514", ex.SqlState);
        }

        [Fact]
        public async Task ParticipantEventId_MustMatchItsStream()
        {
            var (eventId, host) = await OnlineEvent();
            var (otherEvent, _) = await OnlineEvent();
            var stream = await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"insert into {S}.video_participants (event_id, participant_id, video_stream_id) " +
                "values (@event, @user, @stream)",
                ("event", otherEvent), ("user", host), ("stream", stream)));
            Assert.Equal("23503", ex.SqlState);
        }

        [Fact]
        public async Task VideoStreamEntity_RoundTripsThroughPostgrest()
        {
            var (eventId, host) = await OnlineEvent();
            var id = Guid.NewGuid();

            await _fx.Client.From<VideoStream>().Insert(
                new VideoStream { Id = id, EventId = eventId, HostId = host, RoomName = VideoStream.RoomNameFor(id) },
                new Supabase.Postgrest.QueryOptions { Returning = ReturnType.Minimal });

            var loaded = await _fx.Client.From<VideoStream>()
                .Filter("id", Operator.Equals, id.ToString())
                .Single();

            Assert.Equal($"stream-{id}", loaded!.RoomName);
            Assert.Null(loaded.EndedAt);
        }
    }
}
