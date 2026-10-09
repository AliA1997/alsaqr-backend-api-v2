using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Domain.Common;
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamParticipationTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private VideoScenario _s = default!;
        private VideoStream _stream = default!;

        public VideoStreamParticipationTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _s = await VideoScenario.Create(_fx.Seed);
            _stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private Task SetLeft(Guid userId, string reason, string ago) =>
            _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '{ago}', left_reason = @r, sfu_identity = null " +
                "where video_stream_id = @s and participant_id = @u",
                ("r", reason), ("s", _stream.Id), ("u", userId));

        private Task SetRole(Guid userId, string role) =>
            _fx.Seed.Execute(
                $"update {S}.video_participants set role = @r where video_stream_id = @s and participant_id = @u",
                ("r", role), ("s", _stream.Id), ("u", userId));

        [Fact]
        public async Task Join_NonAttendee_ThrowsForbidden()
        {
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.JoinEventStream(_fx.Client, _s.OutsiderId, _s.EventId, default));
        }

        [Fact]
        public async Task Join_Attendee_EntersAsViewer_WithStreamScopedIdentity()
        {
            var (stream, participant, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(_stream.Id, stream.Id);
            Assert.Equal(VideoParticipant.RoleViewer, participant.Role);
            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", participant.SfuIdentity);
            Assert.Null(participant.LeftAt);
        }

        [Fact]
        public async Task Join_WhenNoLiveStream_ThrowsNotFound()
        {
            await _repo.EndStreamSystem(_fx.Client, _stream, default);

            await Assert.ThrowsAsync<NotFoundException>(
                () => _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task Join_FounderWhoIsNotAnAttendee_JoinsAsHost()
        {
            await _repo.EndStreamSystem(_fx.Client, _stream, default);
            await _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            var (_, participant, _) = await _repo.JoinEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleHost, participant.Role);
        }

        [Fact]
        public async Task DuplicateJoin_WhileLive_PreservesPresenterRole()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetRole(_s.AttendeeId, VideoParticipant.RolePresenter);

            var (_, again, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RolePresenter, again.Role);
        }

        [Fact]
        public async Task SecondDevice_DoesNotCreateSecondRow_AndReportsLiveConnection()
        {
            var (_, _, firstHadConnection) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            var (_, _, secondHadConnection) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.False(firstHadConnection);
            Assert.True(secondHadConnection);   // Galène allows duplicate usernames: the controller kicks the older one
            Assert.Equal(1L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where video_stream_id = @s and participant_id = @u",
                ("s", _stream.Id), ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Rejoin_WithinGrace_KeepsPresenterRole()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetRole(_s.AttendeeId, VideoParticipant.RolePresenter);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "4 minutes");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RolePresenter, rejoined.Role);
            Assert.Null(rejoined.LeftAt);
            Assert.Null(rejoined.LeftReason);
        }

        [Fact]
        public async Task Rejoin_AfterGrace_DowngradesToViewer()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetRole(_s.AttendeeId, VideoParticipant.RolePresenter);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "6 minutes");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleViewer, rejoined.Role);
        }

        [Fact]
        public async Task Rejoin_AfterDemotionAndDrop_StaysViewer()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            // A demote persists role = viewer; then the client drops.
            await SetRole(_s.AttendeeId, VideoParticipant.RoleViewer);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "1 minute");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleViewer, rejoined.Role);
        }

        [Fact]
        public async Task HostLeavesAndRejoinsAfterGrace_ResumesHost()
        {
            await SetLeft(_s.OrganizerId, VideoParticipant.LeftExplicit, "10 minutes");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleHost, rejoined.Role);
        }

        [Fact]
        public async Task Leave_ReturnsPreLeaveSnapshot_AndMarksExplicit()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            var left = await _repo.LeaveEventStream(
                _fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default);

            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", left!.Value.Snapshot.SfuIdentity);
            Assert.Equal(_stream.RoomName, left.Value.Stream.RoomName);
            Assert.Equal("explicit", await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where video_stream_id = @s and participant_id = @u",
                ("s", _stream.Id), ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Leave_Twice_SecondReturnsNull()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await _repo.LeaveEventStream(_fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default);

            Assert.Null(await _repo.LeaveEventStream(
                _fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default));
        }

        [Fact]
        public async Task Heartbeat_RefreshesLastSeen()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = now() - interval '50 seconds' where participant_id = @u",
                ("u", _s.AttendeeId));

            await _repo.TouchLiveParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.True(await _fx.Seed.Scalar<bool>(
                $"select last_seen_at > now() - interval '10 seconds' from {S}.video_participants where participant_id = @u",
                ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Heartbeat_AfterReaped_ThrowsForbidden()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "5 seconds");

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.TouchLiveParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task SetCameraState_Persists()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            await _repo.SetCameraState(_fx.Client, _s.AttendeeId, _s.EventId, cameraEnabled: true, muted: false, default);

            var roster = await _repo.GetParticipants(_fx.Client, _stream.Id, default);
            var me = Assert.Single(roster, p => p.ParticipantId == _s.AttendeeId);
            Assert.True(me.CameraEnabled);
            Assert.False(me.Muted);
        }

        [Fact]
        public async Task Roster_IsDeterministicallyOrdered_AndExcludesLeft()
        {
            var extra1 = await _s.AddAttendee(_fx.Seed, "extra1");
            var extra2 = await _s.AddAttendee(_fx.Seed, "extra2");
            foreach (var user in new[] { _s.AttendeeId, extra1, extra2 })
                await _repo.JoinEventStream(_fx.Client, user, _s.EventId, default);
            await _repo.LeaveEventStream(_fx.Client, extra2, _s.EventId, VideoParticipant.LeftExplicit, default);

            // Force a joined_at tie so only the id tie-breaker decides.
            await _fx.Seed.Execute(
                $"update {S}.video_participants set joined_at = '2026-01-01T00:00:00Z' where video_stream_id = @s",
                ("s", _stream.Id));

            var expected = new List<Guid>();
            await using (var conn = new Npgsql.NpgsqlConnection(_fx.ConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = new Npgsql.NpgsqlCommand(
                    $"select participant_id from {S}.video_participants where video_stream_id = @s and left_at is null " +
                    "order by joined_at asc, id asc", conn);
                cmd.Parameters.AddWithValue("s", _stream.Id);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    expected.Add(reader.GetGuid(0));
            }

            var first = (await _repo.GetParticipants(_fx.Client, _stream.Id, default)).Select(p => p.ParticipantId).ToList();
            var second = (await _repo.GetParticipants(_fx.Client, _stream.Id, default)).Select(p => p.ParticipantId).ToList();

            Assert.Equal(expected, first);
            Assert.Equal(first, second);
            Assert.DoesNotContain(extra2, first);
            Assert.Equal("organizer", (await _repo.GetParticipants(_fx.Client, _stream.Id, default))
                .Single(p => p.ParticipantId == _s.OrganizerId).Username);
        }
    }
}
