using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup.Exceptions;
using AlSaqr.Domain.Common;
using AlSaqr.Tests.Support;
using Npgsql;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamControlTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private VideoScenario _s = default!;
        private VideoStream _stream = default!;

        public VideoStreamControlTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _s = await VideoScenario.Create(_fx.Seed);
            _stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Promote_ByHost_MakesViewerPresenter()
        {
            var (stream, promoted) = await _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            Assert.Equal(_stream.RoomName, stream.RoomName);
            Assert.Equal(VideoParticipant.RolePresenter, promoted.Role);
            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", promoted.SfuIdentity);
        }

        [Fact]
        public async Task Promote_ByFounder_IsAllowed()
        {
            var (_, promoted) = await _repo.PromotePresenter(
                _fx.Client, _s.FounderId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            Assert.Equal(VideoParticipant.RolePresenter, promoted.Role);
        }

        [Fact]
        public async Task Promote_ByPlainAttendee_ThrowsForbidden()
        {
            var other = await _s.AddAttendee(_fx.Seed, "other");
            await _repo.JoinEventStream(_fx.Client, other, _s.EventId, default);

            await Assert.ThrowsAsync<ForbiddenException>(() => _repo.PromotePresenter(
                _fx.Client, _s.AttendeeId, _s.EventId, other, maxPresenters: 9, default));
        }

        [Fact]
        public async Task Promote_TargetNotLive_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.OutsiderId, maxPresenters: 9, default));
        }

        [Fact]
        public async Task Promote_BeyondPresenterCap_ThrowsConflict()
        {
            // Cap of 2: the host is one publisher, so exactly one promotion fits.
            var second = await _s.AddAttendee(_fx.Seed, "second");
            await _repo.JoinEventStream(_fx.Client, second, _s.EventId, default);
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 2, default);

            await Assert.ThrowsAsync<ConflictException>(() => _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, second, maxPresenters: 2, default));
        }

        [Fact]
        public async Task Promote_AlreadyPresenter_IsIdempotentAndIgnoresCap()
        {
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 2, default);

            var (_, again) = await _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 2, default);

            Assert.Equal(VideoParticipant.RolePresenter, again.Role);
        }

        [Fact]
        public async Task Promote_WritesOnlyRole_LeavingConcurrentCameraStateAlone()
        {
            await _repo.SetCameraState(_fx.Client, _s.AttendeeId, _s.EventId, cameraEnabled: true, muted: false, default);

            var (_, promoted) = await _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            Assert.True(promoted.CameraEnabled);
            Assert.False(promoted.Muted);
        }

        [Fact]
        public async Task Demote_Presenter_BecomesViewer()
        {
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            var (_, demoted) = await _repo.DemotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, default);

            Assert.Equal(VideoParticipant.RoleViewer, demoted.Role);
            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", demoted.SfuIdentity);
        }

        [Fact]
        public async Task Demote_Host_ThrowsValidation()
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => _repo.DemotePresenter(_fx.Client, _s.FounderId, _s.EventId, _s.OrganizerId, default));
        }

        [Fact]
        public async Task End_ByPlainAttendee_ThrowsForbidden()
        {
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.EndEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task End_ByHost_EndsStream_AndMarksEveryoneEnded()
        {
            var ended = await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.NotNull(ended.EndedAt);
            Assert.Equal(_stream.RoomName, ended.RoomName);
            Assert.Equal(0L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where video_stream_id = @s and left_at is null",
                ("s", _stream.Id)));
            Assert.Equal(2L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where video_stream_id = @s and left_reason = 'ended'",
                ("s", _stream.Id)));
        }

        [Fact]
        public async Task End_ByFounder_EndsAStreamSomeoneElseStarted()
        {
            var ended = await _repo.EndEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            Assert.NotNull(ended.EndedAt);
        }

        [Fact]
        public async Task EndStreamSystem_OnAlreadyEndedStream_ThrowsConflict_AndKeepsFirstEndTime()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            var firstEnd = await _fx.Seed.Scalar<DateTime>(
                $"select ended_at from {S}.video_streams where id = @s", ("s", _stream.Id));

            await Assert.ThrowsAsync<ConflictException>(() => _repo.EndStreamSystem(_fx.Client, _stream, default));

            Assert.Equal(firstEnd, await _fx.Seed.Scalar<DateTime>(
                $"select ended_at from {S}.video_streams where id = @s", ("s", _stream.Id)));
        }

        [Fact]
        public async Task Kick_MarksLeftKicked_ResetsRole_ClearsIdentity()
        {
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            var kicked = await _repo.KickParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.NotNull(kicked);
            Assert.Equal(VideoParticipant.LeftKicked, kicked!.Value.Participant.LeftReason);
            Assert.NotNull(kicked.Value.Participant.LeftAt);
            Assert.Equal(VideoParticipant.RoleViewer, kicked.Value.Participant.Role);
            Assert.Null(kicked.Value.Participant.SfuIdentity);
        }

        [Fact]
        public async Task Kick_UserNotLive_ReturnsNull()
        {
            Assert.Null(await _repo.KickParticipant(_fx.Client, _s.OutsiderId, _s.EventId, default));
        }

        [Fact]
        public async Task Kick_AlreadyLeft_IsNotRewritten()
        {
            await _repo.LeaveEventStream(_fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default);

            Assert.Null(await _repo.KickParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default));
            Assert.Equal(VideoParticipant.LeftExplicit, await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task KickedPresenter_RejoiningStillAttending_ComesBackViewer_NotPresenter()
        {
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);
            await _repo.KickParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default);

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleViewer, rejoined.Role);
        }

        [Fact]
        public async Task KickedAttendeeRemovedFromEvent_CannotRejoin()
        {
            await _fx.Seed.Execute(
                $"delete from {S}.event_attendees where attendee_id = (select id from {S}.attendees where user_id = @u)",
                ("u", _s.AttendeeId));
            await _repo.KickParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task RemoveEventAttendee_MarksTheirLiveParticipantRowKicked()
        {
            var attendees = new AttendeeRepository();
            var events = new EventAttendeeRepository(new GroupAttendeeRepository(attendees), attendees, _repo);
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            await events.RemoveEventAttendee(_fx.Client, _s.FounderId, _s.EventId, _s.AttendeeId, default);

            Assert.Equal(VideoParticipant.LeftKicked, await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", _s.AttendeeId)));
            Assert.Equal(VideoParticipant.RoleViewer, await _fx.Seed.Scalar<string>(
                $"select role from {S}.video_participants where participant_id = @u", ("u", _s.AttendeeId)));
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        private sealed class FailingKickRepository : VideoStreamRepository
        {
            public override Task<(VideoStream Stream, VideoParticipant Participant)?> KickParticipant(
                Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct) =>
                throw new InvalidOperationException("kick failed");
        }

        private static EventAttendeeRepository Events(IVideoStreamRepository video)
        {
            var attendees = new AttendeeRepository();
            return new EventAttendeeRepository(new GroupAttendeeRepository(attendees), attendees, video);
        }

        [Fact]
        public async Task RemoveEventAttendee_KickFails_AttendanceKept_LiveRowUnchanged_AndRetryable()
        {
            await Assert.ThrowsAsync<UnjoinEventException>(() =>
                Events(new FailingKickRepository()).RemoveEventAttendee(
                    _fx.Client, _s.FounderId, _s.EventId, _s.AttendeeId, default));

            Assert.Equal(1L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.event_attendees ea join {S}.attendees a on a.id = ea.attendee_id " +
                "where a.user_id = @u and ea.event_id = @e", ("u", _s.AttendeeId), ("e", _s.EventId)));
            Assert.Equal(1L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where participant_id = @u and left_at is null",
                ("u", _s.AttendeeId)));

            await Events(_repo).RemoveEventAttendee(_fx.Client, _s.FounderId, _s.EventId, _s.AttendeeId, default);

            Assert.Equal(VideoParticipant.LeftKicked, await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task RemoveEventAttendee_NoAttendeeRecord_StillKicksLiveRow()
        {
            // A founder can be live without being an event attendee or having an attendees row.
            await _repo.JoinEventStream(_fx.Client, _s.FounderId, _s.EventId, default);
            await _fx.Seed.Execute($"delete from {S}.attendees where user_id = @u", ("u", _s.FounderId));

            await Events(_repo).RemoveEventAttendee(_fx.Client, _s.FounderId, _s.EventId, _s.FounderId, default);

            Assert.Equal(VideoParticipant.LeftKicked, await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", _s.FounderId)));
        }

        [Fact]
        public async Task KickedIsAValidLeftReason_UnknownIsStillRejected()
        {
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now(), left_reason = 'kicked' where participant_id = @u",
                ("u", _s.AttendeeId));

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now(), left_reason = 'banned' where participant_id = @u",
                ("u", _s.OrganizerId)));
            Assert.Equal("23514", ex.SqlState);
        }

        [Fact]
        public async Task GetStaleLiveParticipants_UsesCutoffInSql()
        {
            await _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = now() - interval '61 seconds' where participant_id = @u",
                ("u", _s.AttendeeId));

            var stale = await _repo.GetStaleLiveParticipants(
                _fx.Client, _stream.Id, DateTime.UtcNow.AddSeconds(-60), default);

            Assert.Equal(_s.AttendeeId, Assert.Single(stale).ParticipantId);
        }

        [Fact]
        public async Task GetStaleLiveParticipants_OrdersByJoinedAt_ThenId_AndSkipsLeft()
        {
            var third = await _s.AddAttendee(_fx.Seed, "third");
            await _repo.JoinEventStream(_fx.Client, third, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = timestamptz '2026-01-01 00:00:00+00', " +
                "joined_at = timestamptz '2026-01-01 00:00:00+00'");
            await _fx.Seed.Execute(
                $"update {S}.video_participants set joined_at = timestamptz '2025-12-31 00:00:00+00' where participant_id = @u",
                ("u", third));
            await _repo.LeaveEventStream(_fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default);

            var stale = await _repo.GetStaleLiveParticipants(
                _fx.Client, _stream.Id, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), default);

            Assert.Equal(2, stale.Count);
            Assert.Equal(third, stale[0].ParticipantId);
            Assert.Equal(_s.OrganizerId, stale[1].ParticipantId);
        }

        [Fact]
        public async Task GetStaleLiveParticipants_CutoffBoundary_IsExclusive()
        {
            await _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = timestamptz '2026-01-01 00:00:00+00'");
            var cutoff = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.Empty(await _repo.GetStaleLiveParticipants(_fx.Client, _stream.Id, cutoff, default));
            Assert.Equal(2, (await _repo.GetStaleLiveParticipants(_fx.Client, _stream.Id, cutoff.AddSeconds(1), default)).Count);
        }

        [Fact]
        public async Task GetLiveStreamsStartedBefore_FindsOnlyOldStreams()
        {
            Assert.Empty(await _repo.GetLiveStreamsStartedBefore(_fx.Client, DateTime.UtcNow.AddHours(-4), default));

            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = now() - interval '4 hours 1 minute' where id = @s",
                ("s", _stream.Id));

            Assert.Single(await _repo.GetLiveStreamsStartedBefore(_fx.Client, DateTime.UtcNow.AddHours(-4), default));
        }

        [Fact]
        public async Task GetLiveStreams_ReturnsOnlyLive_OldestFirst_TieBrokenById()
        {
            var secondEvent = await _fx.Seed.Event(_s.GroupId, isOnline: true);
            var thirdEvent = await _fx.Seed.Event(_s.GroupId, isOnline: true);
            var endedEvent = await _fx.Seed.Event(_s.GroupId, isOnline: true);
            var second = await _repo.StartEventStream(_fx.Client, _s.FounderId, secondEvent, default);
            var third = await _repo.StartEventStream(_fx.Client, _s.FounderId, thirdEvent, default);
            await _repo.StartEventStream(_fx.Client, _s.FounderId, endedEvent, default);
            await _repo.EndEventStream(_fx.Client, _s.FounderId, endedEvent, default);
            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = timestamptz '2026-01-01 00:00:00+00' where ended_at is null");
            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = timestamptz '2025-01-01 00:00:00+00' where id = @s",
                ("s", third.Id));

            var live = await _repo.GetLiveStreams(_fx.Client, default);

            var tied = new[] { _stream.Id, second.Id }.OrderBy(id => id).ToList();
            Assert.Equal(new[] { third.Id }.Concat(tied), live.Select(v => v.Id));
        }

        [Fact]
        public async Task Purge_DeletesParticipantsOlderThanCutoff_KeepsNewer()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '91 days' where participant_id = @old",
                ("old", _s.AttendeeId));
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '89 days' where participant_id = @kept",
                ("kept", _s.OrganizerId));

            var purged = await _repo.PurgeParticipantsLeftBefore(_fx.Client, DateTime.UtcNow.AddDays(-90), default);

            Assert.Equal(1, purged);
            Assert.Equal(1L, await _fx.Seed.Scalar<long>($"select count(*) from {S}.video_participants"));
            Assert.Equal(_s.OrganizerId, await _fx.Seed.Scalar<Guid>($"select participant_id from {S}.video_participants"));
        }

        [Fact]
        public async Task Purge_CutoffBoundary_KeepsRowLeftExactlyAtCutoff()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = timestamptz '2026-01-01 00:00:00+00' where participant_id = @u",
                ("u", _s.AttendeeId));
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = timestamptz '2025-12-31 23:59:59+00' where participant_id = @u",
                ("u", _s.OrganizerId));

            var purged = await _repo.PurgeParticipantsLeftBefore(
                _fx.Client, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), default);

            Assert.Equal(1, purged);
            Assert.Equal(_s.AttendeeId, await _fx.Seed.Scalar<Guid>($"select participant_id from {S}.video_participants"));
        }

        [Fact]
        public async Task Purge_NothingOldEnough_ReturnsZero_AndKeepsLiveStream()
        {
            var purged = await _repo.PurgeParticipantsLeftBefore(_fx.Client, DateTime.UtcNow.AddDays(-90), default);

            Assert.Equal(0, purged);
            Assert.Equal(1L, await _fx.Seed.Scalar<long>($"select count(*) from {S}.video_streams"));
        }

        [Fact]
        public async Task Purge_DeletesStreamsEndedBeforeCutoff()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '91 days'");
            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = now() - interval '92 days', ended_at = now() - interval '91 days'");

            await _repo.PurgeParticipantsLeftBefore(_fx.Client, DateTime.UtcNow.AddDays(-90), default);

            Assert.Equal(0L, await _fx.Seed.Scalar<long>($"select count(*) from {S}.video_streams"));
        }
    }
}
