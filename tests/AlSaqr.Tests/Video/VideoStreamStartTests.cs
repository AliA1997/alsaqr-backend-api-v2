using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Domain.Common;
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamStartTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private VideoScenario _s = default!;

        public VideoStreamStartTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _s = await VideoScenario.Create(_fx.Seed);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private Task<long> LiveStreamCount(Guid eventId) =>
            _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_streams where event_id = @e and ended_at is null", ("e", eventId));

        [Fact]
        public async Task Start_InPersonEvent_ThrowsValidation()
        {
            var inPerson = await _fx.Seed.Event(_s.GroupId, isOnline: false);

            await Assert.ThrowsAsync<ValidationException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, inPerson, default));
        }

        [Fact]
        public async Task Start_GrouplessEvent_ThrowsValidation()
        {
            var groupless = await _fx.Seed.Event(null, isOnline: true);

            await Assert.ThrowsAsync<ValidationException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, groupless, default));
        }

        [Fact]
        public async Task Start_UnknownEvent_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, Guid.NewGuid(), default));
        }

        [Theory]
        [InlineData("attendee")]
        [InlineData("outsider")]
        public async Task Start_ByNonOrganizer_ThrowsForbidden(string who)
        {
            var user = who == "attendee" ? _s.AttendeeId : _s.OutsiderId;

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.StartEventStream(_fx.Client, user, _s.EventId, default));
        }

        [Fact]
        public async Task Start_ByEventOrganizer_CreatesLiveStreamWithHostRow()
        {
            var stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.Equal(VideoStream.RoomNameFor(stream.Id), stream.RoomName);
            Assert.Equal(_s.OrganizerId, stream.HostId);
            var host = Assert.Single(await _repo.GetLiveParticipants(_fx.Client, stream.Id, default));
            Assert.Equal(VideoParticipant.RoleHost, host.Role);
            Assert.Equal(_s.OrganizerId, host.ParticipantId);
        }

        [Fact]
        public async Task Start_ByGroupFounder_Succeeds()
        {
            var stream = await _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            Assert.Equal(_s.FounderId, stream.HostId);
        }

        [Fact]
        public async Task Start_WhileLive_ThrowsConflict()
        {
            await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            await Assert.ThrowsAsync<ConflictException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default));
        }

        [Fact]
        public async Task Start_Concurrently_OneWins_OtherGetsConflictNot500()
        {
            var results = await Task.WhenAll(
                Attempt(() => _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default)),
                Attempt(() => _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default)));

            Assert.Single(results, r => r == null);
            Assert.Single(results, r => r is ConflictException);
            Assert.Equal(1L, await LiveStreamCount(_s.EventId));
        }

        [Fact]
        public async Task Start_WhenHostRowInsertFails_EndsStreamAndRethrows()
        {
            await _fx.Seed.Execute($"alter table {S}.video_participants add constraint zz_block check (false) not valid");
            try
            {
                await Assert.ThrowsAnyAsync<Exception>(
                    () => _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default));
            }
            finally
            {
                await _fx.Seed.Execute($"alter table {S}.video_participants drop constraint zz_block");
            }

            Assert.Equal(0L, await LiveStreamCount(_s.EventId));
            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
        }

        [Fact]
        public async Task NewStreamAfterEnd_HasDifferentRoomName()
        {
            var first = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.EndStreamSystem(_fx.Client, first, default);

            var second = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.NotEqual(first.RoomName, second.RoomName);
        }

        [Fact]
        public async Task EndStreamSystem_StampsEndedAt_AndMarksParticipantsEnded()
        {
            var stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            var ended = await _repo.EndStreamSystem(_fx.Client, stream, default);

            Assert.NotNull(ended.EndedAt);
            Assert.Equal(0L, await LiveStreamCount(_s.EventId));
            Assert.Equal("ended", await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where video_stream_id = @s", ("s", stream.Id)));
        }

        [Fact]
        public async Task GetLiveEventStream_ReturnsDisplay_OrNullWhenNone()
        {
            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));

            var stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            var display = await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default);

            Assert.Equal(stream.Id, display!.VideoStreamId);
            Assert.Equal("organizer", display.HostUsername);
            Assert.Equal("Test Event", display.EventName);
            Assert.Equal(1, display.ParticipantCount);
            Assert.True(display.IsLive);
        }

        [Fact]
        public async Task RequireLiveEventStream_WhenNone_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _repo.RequireLiveEventStream(_fx.Client, _s.EventId, default));
        }

        private static async Task<Exception?> Attempt(Func<Task> action)
        {
            try { await action(); return null; }
            catch (Exception ex) { return ex; }
        }
    }
}
