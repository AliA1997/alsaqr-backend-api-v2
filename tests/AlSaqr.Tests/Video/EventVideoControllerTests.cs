using System.Net;
using System.Net.Http.Json;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class EventVideoControllerTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private EventVideoApi _api = default!;
        private VideoScenario _s = default!;

        public EventVideoControllerTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _api = await EventVideoApi.StartAsync(_fx.Client);
            _s = await VideoScenario.Create(_fx.Seed);
        }

        public async Task DisposeAsync() => await _api.DisposeAsync();

        private string Route(string action) => $"/api/EventVideo/event/{_s.EventId}/{action}";

        private async Task Start()
        {
            var response = await _api.Post(Route("start"), _s.OrganizerId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private Task<long> LiveRows(Guid userId) =>
            _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where participant_id = @u and left_at is null",
                ("u", userId));

        private Task<string> RoleOf(Guid userId) =>
            _fx.Seed.Scalar<string>(
                $"select role from {S}.video_participants where participant_id = @u and left_at is null",
                ("u", userId));

        [Fact]
        public async Task Start_ByOrganizer_CreatesRoomWithCostGuards_AndBroadcasts()
        {
            var response = await _api.Post(Route("start"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var stream = await response.Content.ReadFromJsonAsync<VideoStreamToDisplay>();
            Assert.True(stream!.IsLive);
            Assert.Equal($"CreateRoom stream-{stream.VideoStreamId} max=50", Assert.Single(_api.Sfu.Calls));
            Assert.Contains(_api.Broadcaster.Events, e => e.Name == "stream_started" && e.Subject == stream.VideoStreamId);
        }

        [Fact]
        public async Task Start_InPersonEvent_Returns400ProblemDetails_AndMakesNoSfuCall()
        {
            var inPerson = await _fx.Seed.Event(_s.GroupId, isOnline: false);

            var response = await _api.Post($"/api/EventVideo/event/{inPerson}/start", _s.FounderId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Empty(_api.Sfu.Calls);
        }

        [Fact]
        public async Task Start_WhenCreateRoomFails_Returns500_AndLeavesNoLiveStream()
        {
            _api.Sfu.FailCreateRoom = true;

            var response = await _api.Post(Route("start"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            // No live stream: Ok(null) is written as 204 by ASP.NET's no-content formatter.
            var live = await _api.Get(Route("live"), _s.OrganizerId);
            Assert.Equal(HttpStatusCode.NoContent, live.StatusCode);

            _api.Sfu.FailCreateRoom = false;
            Assert.Equal(HttpStatusCode.OK, (await _api.Post(Route("start"), _s.OrganizerId)).StatusCode);
        }

        [Fact]
        public async Task Join_WithForgedToken_Returns401_AndMintsNoToken()
        {
            await Start();
            var forged = new TokenService("some-other-secret-0123456789-abcdefghijklmn")
                .GenerateTokens(_s.AttendeeId.ToString()).AccessToken;

            var response = await _api.PostWithRawToken(Route("join"), forged);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Join_WithoutToken_Returns401_AndMintsNoToken()
        {
            await Start();

            var response = await _api.Http.PostAsync(Route("join"), JsonContent.Create(new { }));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Join_NonAttendee_Returns403ProblemDetails_AndMintsNoToken()
        {
            await Start();

            var response = await _api.Post(Route("join"), _s.OutsiderId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(403, problem!.Status);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Join_Attendee_ReturnsViewerToken_RosterAndFullBroadcast()
        {
            await Start();

            var response = await _api.Post(Route("join"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JoinVideoStreamResultDto>();
            Assert.Equal("viewer", result!.Role);
            Assert.False(result.CanPublish);
            Assert.Equal("ws://galene.test/ws", result.WsUrl);
            Assert.Equal($"stream-{result.Stream.VideoStreamId}", result.Group);
            Assert.Contains(result.Participants, p => p.ParticipantId == _s.AttendeeId && p.Username == "attendee");
            var minted = Assert.Single(_api.Sfu.MintedTokens);
            Assert.Equal($"{result.Stream.VideoStreamId}:{_s.AttendeeId}", minted.Identity);
            Assert.False(minted.CanPublish);
            Assert.DoesNotContain(_api.Sfu.Calls, c => c.StartsWith("Remove"));
            // The broadcast carries the roster entry, so other clients need no refetch.
            Assert.Contains(_api.Broadcaster.Events,
                e => e.Name == "participant_joined" && e.Subject == _s.AttendeeId && e.Detail == "attendee");
        }

        [Fact]
        public async Task Join_Again_KicksOlderConnection()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var again = await _api.Post(Route("join"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            var kick = Assert.Single(_api.Sfu.Calls);
            Assert.StartsWith("Remove stream-", kick);
            Assert.EndsWith($":{_s.AttendeeId}", kick);
            Assert.Equal(2, _api.Sfu.MintedTokens.Count);
        }

        [Fact]
        public async Task Join_DemotedWhileOlderConnectionIsKicked_MintsViewerToken()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.OrganizerId);
            // The demote lands after the join write but before the token is minted.
            _api.Sfu.BeforeRemove = () => new VideoStreamRepository()
                .DemotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, default);

            var response = await _api.Post(Route("join"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JoinVideoStreamResultDto>();
            Assert.Equal("viewer", result!.Role);
            Assert.False(result.CanPublish);
            Assert.False(_api.Sfu.MintedTokens[^1].CanPublish);
        }

        [Fact]
        public async Task Token_ForLiveParticipant_ReturnsFreshToken_WithoutBroadcast()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            var eventsBefore = _api.Broadcaster.Events.Count;

            var response = await _api.Post(Route("token"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var token = await response.Content.ReadFromJsonAsync<VideoTokenDto>();
            Assert.Equal("viewer", token!.Role);
            Assert.False(token.CanPublish);
            Assert.False(string.IsNullOrEmpty(token.Token));
            Assert.StartsWith("stream-", token.Group);
            Assert.Equal(eventsBefore, _api.Broadcaster.Events.Count);
        }

        [Fact]
        public async Task Token_AfterPromoteThenDemote_FollowsTheCurrentRole()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.OrganizerId);

            var asPresenter = await (await _api.Post(Route("token"), _s.AttendeeId))
                .Content.ReadFromJsonAsync<VideoTokenDto>();
            await _api.Post(Route($"presenters/{_s.AttendeeId}/demote"), _s.OrganizerId);
            var asViewer = await (await _api.Post(Route("token"), _s.AttendeeId))
                .Content.ReadFromJsonAsync<VideoTokenDto>();

            Assert.True(asPresenter!.CanPublish);
            Assert.False(asViewer!.CanPublish);
            Assert.Equal(new[] { true, false }, _api.Sfu.MintedTokens.Skip(1).Select(t => t.CanPublish));
        }

        [Fact]
        public async Task Token_WhenNotLive_Returns403()
        {
            await Start();

            var response = await _api.Post(Route("token"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Live_ForAttendee_ReturnsDisplayWithoutSfuValues()
        {
            await Start();

            var response = await _api.Get(Route("live"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("stream-", body);
            Assert.DoesNotContain("room", body, StringComparison.OrdinalIgnoreCase);
            var stream = await response.Content.ReadFromJsonAsync<VideoStreamToDisplay>();
            Assert.True(stream!.IsLive);
            Assert.Equal(_s.EventId, stream.EventId);
        }

        [Fact]
        public async Task Live_ForNonAttendee_Returns403_EvenWhenLive()
        {
            await Start();

            var response = await _api.Get(Route("live"), _s.OutsiderId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Leave_RemovesOnSfu_AndBroadcasts_SecondLeaveIsNoOp()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var first = await _api.Post(Route("leave"), _s.AttendeeId);
            var second = await _api.Post(Route("leave"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
            Assert.Single(_api.Sfu.Calls, c => c.StartsWith("Remove") && c.EndsWith($":{_s.AttendeeId}"));
            Assert.Single(_api.Broadcaster.Events, e => e.Name == "participant_left" && e.Detail == "explicit");
        }

        [Fact]
        public async Task Leave_WhenSfuRemovalFails_Returns500_StaysLive_AndRetrySucceeds()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.FailRoomTeardown = _ => new InvalidOperationException("Galène operator unreachable.");

            var failed = await _api.Post(Route("leave"), _s.AttendeeId);

            // SFU first: the row stays live, so the retry (or the reaper) still finds it.
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal(1L, await LiveRows(_s.AttendeeId));
            Assert.DoesNotContain(_api.Broadcaster.Events, e => e.Name == "participant_left");

            _api.Sfu.FailRoomTeardown = null;
            var retry = await _api.Post(Route("leave"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
            Assert.Equal(0L, await LiveRows(_s.AttendeeId));
            Assert.Single(_api.Broadcaster.Events, e => e.Name == "participant_left");
        }

        [Fact]
        public async Task Leave_WhenBroadcastFails_StillReturns204_AndIsLeft()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Broadcaster.ParticipantLeftFailure = new HttpRequestException("realtime down");

            var response = await _api.Post(Route("leave"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(0L, await LiveRows(_s.AttendeeId));
        }

        [Fact]
        public async Task Promote_GrantsPublishOnSfu_ThenDemote_RevokesIt()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var promote = await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.OrganizerId);
            var demote = await _api.Post(Route($"presenters/{_s.AttendeeId}/demote"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, demote.StatusCode);
            Assert.Collection(_api.Sfu.Calls,
                c => Assert.EndsWith($":{_s.AttendeeId} True", c),
                c => Assert.EndsWith($":{_s.AttendeeId} False", c));
            Assert.Equal(2, _api.Broadcaster.Events.Count(e => e.Name == "role_changed"));
        }

        [Fact]
        public async Task Promote_ByPlainAttendee_Returns403_AndMakesNoSfuCall()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var response = await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(_api.Sfu.Calls);
            Assert.Equal("viewer", await RoleOf(_s.AttendeeId));
        }

        [Fact]
        public async Task Demote_WhenTargetHasNoLiveConnection_Succeeds_AsAlreadyGone()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.OrganizerId);
            _api.Sfu.FailSetPublish = room => new ParticipantNotConnectedException(room);

            var response = await _api.Post(Route($"presenters/{_s.AttendeeId}/demote"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("viewer", await RoleOf(_s.AttendeeId));
            Assert.Contains(_api.Broadcaster.Events,
                e => e.Name == "role_changed" && e.Subject == _s.AttendeeId && e.Detail == "viewer");
        }

        [Fact]
        public async Task Demote_WhenSfuFails_Returns500_RoleAlreadyViewer_AndRetryRevokesAgain()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.OrganizerId);
            _api.Sfu.Calls.Clear();
            _api.Sfu.FailSetPublish = _ => new InvalidOperationException("Galène refused the publish change.");

            var failed = await _api.Post(Route($"presenters/{_s.AttendeeId}/demote"), _s.OrganizerId);

            // Database first: any token minted from now on is receive-only.
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal("viewer", await RoleOf(_s.AttendeeId));
            Assert.DoesNotContain(_api.Broadcaster.Events, e => e.Name == "role_changed" && e.Detail == "viewer");

            _api.Sfu.FailSetPublish = null;
            var retry = await _api.Post(Route($"presenters/{_s.AttendeeId}/demote"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
            Assert.Equal(2, _api.Sfu.Calls.Count(c => c.EndsWith($":{_s.AttendeeId} False")));
            Assert.Contains(_api.Broadcaster.Events, e => e.Name == "role_changed" && e.Detail == "viewer");
        }

        [Fact]
        public async Task End_DeletesRoom_AndBroadcastsStreamEnded()
        {
            await Start();
            _api.Sfu.Calls.Clear();

            var response = await _api.Post(Route("end"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.StartsWith("DeleteRoom stream-", Assert.Single(_api.Sfu.Calls));
            Assert.Contains(_api.Broadcaster.Events, e => e.Name == "stream_ended");
        }

        [Fact]
        public async Task End_ByPlainAttendee_Returns403_AndMakesNoSfuCall()
        {
            await Start();
            _api.Sfu.Calls.Clear();

            var response = await _api.Post(Route("end"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(_api.Sfu.Calls);
            Assert.Equal(HttpStatusCode.OK, (await _api.Get(Route("live"), _s.AttendeeId)).StatusCode);
        }

        [Fact]
        public async Task End_WhenDeleteRoomFails_Returns500_StaysLive_AndRetryEnds()
        {
            await Start();
            _api.Sfu.FailRoomTeardown = _ => new InvalidOperationException("Galène admin DELETE failed with status 503.");

            var failed = await _api.Post(Route("end"), _s.OrganizerId);

            // SFU first: the stream stays live, so the host can retry.
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _api.Get(Route("live"), _s.OrganizerId)).StatusCode);
            Assert.DoesNotContain(_api.Broadcaster.Events, e => e.Name == "stream_ended");

            _api.Sfu.FailRoomTeardown = null;
            var retry = await _api.Post(Route("end"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await _api.Get(Route("live"), _s.OrganizerId)).StatusCode);
            Assert.Single(_api.Broadcaster.Events, e => e.Name == "stream_ended");
        }

        [Fact]
        public async Task End_WhenEndedConcurrently_Returns409_WithoutASecondBroadcast()
        {
            await Start();
            _api.Sfu.BeforeDeleteRoom = () => _fx.Seed.Execute(
                $"update {S}.video_streams set ended_at = now() where event_id = @e", ("e", _s.EventId));

            var response = await _api.Post(Route("end"), _s.OrganizerId);

            // Whoever ended it first announced it; this caller learns it is already over.
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain(_api.Broadcaster.Events, e => e.Name == "stream_ended");
        }

        [Fact]
        public async Task Heartbeat_WhenNotLive_Returns403()
        {
            await Start();

            var response = await _api.Post(Route("heartbeat"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Camera_PersistsStateForCaller()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);

            var response = await _api.Post(Route("camera"), _s.AttendeeId,
                new { values = new { cameraEnabled = true, muted = false } });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(await _fx.Seed.Scalar<bool>(
                "select camera_enabled from \"alsaqr-2026\".video_participants where participant_id = @u",
                ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Camera_BindsMutedUnderTheAppJsonDefaults()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);

            // A new row starts muted, so false here can only come from the request body.
            await _api.Post(Route("camera"), _s.AttendeeId, new { values = new { cameraEnabled = true, muted = false } });

            Assert.False(await _fx.Seed.Scalar<bool>(
                $"select muted from {S}.video_participants where participant_id = @u", ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Camera_WithoutValues_Returns400ProblemDetails()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);

            var response = await _api.Post(Route("camera"), _s.AttendeeId, new { });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
