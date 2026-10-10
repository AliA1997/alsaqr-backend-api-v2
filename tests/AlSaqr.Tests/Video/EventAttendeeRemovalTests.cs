using System.Net;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// DELETE /Events/{eventId}/members/{userId}: removing an attendee also cuts their
    /// live video connection (SFU first, then the database), and never breaks for
    /// in-person events or a deployment without the video tables.
    /// </summary>
    [Collection(PostgrestCollection.Name)]
    public class EventAttendeeRemovalTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _video = new();
        private EventVideoApi _api = default!;
        private VideoScenario _s = default!;

        public EventAttendeeRemovalTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _api = await EventVideoApi.StartAsync(_fx.Client);
            _s = await VideoScenario.Create(_fx.Seed);
        }

        public async Task DisposeAsync() => await _api.DisposeAsync();

        private string Members(Guid userId, Guid? eventId = null) =>
            $"/api/Events/{eventId ?? _s.EventId}/members/{userId}";

        private async Task<VideoStream> StartWithAttendeeLive()
        {
            var stream = await _video.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _video.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            return stream;
        }

        private Task<long> Attending(Guid userId, Guid? eventId = null) =>
            _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.event_attendees ea join {S}.attendees a on a.id = ea.attendee_id " +
                "where a.user_id = @u and ea.event_id = @e",
                ("u", userId), ("e", eventId ?? _s.EventId));

        private Task<long> LiveRows(Guid userId) =>
            _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where participant_id = @u and left_at is null",
                ("u", userId));

        private Task<string> LeftReason(Guid userId) =>
            _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", userId));

        [Fact]
        public async Task RemoveLiveAttendee_DisconnectsOnSfu_KicksRow_RemovesAttendance_AndBroadcastsLeft()
        {
            var stream = await StartWithAttendeeLive();

            var response = await _api.Delete(Members(_s.AttendeeId), _s.FounderId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // The identity was captured before the database kick cleared it.
            Assert.Equal($"Remove {stream.RoomName} {stream.Id}:{_s.AttendeeId}", Assert.Single(_api.Sfu.Calls));
            Assert.Equal(VideoParticipant.LeftKicked, await LeftReason(_s.AttendeeId));
            Assert.Equal(0L, await Attending(_s.AttendeeId));
            var left = Assert.Single(_api.Broadcaster.Events, e => e.Name == "participant_left");
            Assert.Equal((_s.EventId, (Guid?)_s.AttendeeId, (string?)VideoParticipant.LeftKicked),
                (left.EventId, left.Subject, left.Detail));
        }

        [Fact]
        public async Task Remove_WhenSfuRemovalFails_Returns500_ChangesNothing_AndRetrySucceeds()
        {
            await StartWithAttendeeLive();
            _api.Sfu.FailRoomTeardown = _ => new InvalidOperationException("Galène operator unreachable.");

            var failed = await _api.Delete(Members(_s.AttendeeId), _s.FounderId);

            // SFU first: nothing in the database changed, so the removal can be retried.
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
            Assert.Equal(1L, await Attending(_s.AttendeeId));
            Assert.Equal(1L, await LiveRows(_s.AttendeeId));
            Assert.DoesNotContain(_api.Broadcaster.Events, e => e.Name == "participant_left");

            _api.Sfu.FailRoomTeardown = null;
            var retry = await _api.Delete(Members(_s.AttendeeId), _s.FounderId);

            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            Assert.Equal(VideoParticipant.LeftKicked, await LeftReason(_s.AttendeeId));
            Assert.Equal(0L, await Attending(_s.AttendeeId));
            Assert.Single(_api.Broadcaster.Events, e => e.Name == "participant_left");
        }

        [Fact]
        public async Task Remove_WhenAlreadyDisconnectedOnSfu_StillKicksAndRemoves()
        {
            await StartWithAttendeeLive();
            _api.Sfu.FailRoomTeardown = room => new ParticipantNotConnectedException(room);

            var response = await _api.Delete(Members(_s.AttendeeId), _s.FounderId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(VideoParticipant.LeftKicked, await LeftReason(_s.AttendeeId));
            Assert.Equal(0L, await Attending(_s.AttendeeId));
        }

        [Fact]
        public async Task Remove_WhenBroadcastFails_StillSucceeds()
        {
            await StartWithAttendeeLive();
            _api.Broadcaster.ParticipantLeftFailure = new HttpRequestException("realtime down");

            var response = await _api.Delete(Members(_s.AttendeeId), _s.FounderId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(VideoParticipant.LeftKicked, await LeftReason(_s.AttendeeId));
            Assert.Equal(0L, await Attending(_s.AttendeeId));
        }

        [Fact]
        public async Task Remove_AttendeeNotLive_RemovesAttendance_WithoutSfuCallOrBroadcast()
        {
            await _video.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            var response = await _api.Delete(Members(_s.AttendeeId), _s.FounderId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(_api.Sfu.Calls);
            Assert.DoesNotContain(_api.Broadcaster.Events, e => e.Name == "participant_left");
            Assert.Equal(0L, await Attending(_s.AttendeeId));
        }

        [Fact]
        public async Task Remove_ByNonFounder_Returns403_AndTouchesNothing()
        {
            await StartWithAttendeeLive();

            // The organizer controls the stream but only the group founder removes attendees.
            var response = await _api.Delete(Members(_s.AttendeeId), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Empty(_api.Sfu.Calls);
            Assert.Equal(1L, await Attending(_s.AttendeeId));
            Assert.Equal(1L, await LiveRows(_s.AttendeeId));
        }

        [Fact]
        public async Task Remove_WithForgedToken_Returns401_AndTouchesNothing()
        {
            await StartWithAttendeeLive();
            var forged = new TokenService("some-other-secret-0123456789-abcdefghijklmn")
                .GenerateTokens(_s.FounderId.ToString()).AccessToken;

            var response = await _api.DeleteWithRawToken(Members(_s.AttendeeId), forged);

            // Identity comes from the signature-verified token; the API's user cache throws if read.
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(_api.Sfu.Calls);
            Assert.Equal(1L, await Attending(_s.AttendeeId));
        }

        [Fact]
        public async Task Remove_UnknownEvent_Returns404()
        {
            var response = await _api.Delete(Members(_s.AttendeeId, Guid.NewGuid()), _s.FounderId);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Remove_FromInPersonEvent_RemovesAttendance_WithoutVideo()
        {
            var inPerson = await _fx.Seed.Event(_s.GroupId, isOnline: false);
            var attendeeRow = await _fx.Seed.Scalar<Guid>(
                $"select id from {S}.attendees where user_id = @u", ("u", _s.AttendeeId));
            await _fx.Seed.EventAttendee(inPerson, attendeeRow, _s.GroupId);

            var response = await _api.Delete(Members(_s.AttendeeId, inPerson), _s.FounderId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0L, await Attending(_s.AttendeeId, inPerson));
            Assert.Equal(1L, await Attending(_s.AttendeeId));
            Assert.Empty(_api.Sfu.Calls);
            Assert.Empty(_api.Broadcaster.Events);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Remove_WhenVideoTablesAreAbsent_StillRemovesAttendance(bool isOnline)
        {
            var eventId = await _fx.Seed.Event(_s.GroupId, isOnline);
            var attendeeRow = await _fx.Seed.Scalar<Guid>(
                $"select id from {S}.attendees where user_id = @u", ("u", _s.AttendeeId));
            await _fx.Seed.EventAttendee(eventId, attendeeRow, _s.GroupId);

            // A deployment that has not run video_streams.sql yet.
            await _fx.Seed.Execute(
                $"alter table {S}.video_participants rename to video_participants_absent; " +
                $"alter table {S}.video_streams rename to video_streams_absent;");
            try
            {
                var response = await _api.Delete(Members(_s.AttendeeId, eventId), _s.FounderId);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(0L, await Attending(_s.AttendeeId, eventId));
                Assert.Empty(_api.Sfu.Calls);
            }
            finally
            {
                await _fx.Seed.Execute(
                    $"alter table {S}.video_participants_absent rename to video_participants; " +
                    $"alter table {S}.video_streams_absent rename to video_streams;");
            }
        }
    }
}
