using AlSaqr.API.HostedServices;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamReaperTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private readonly FakeVideoSfuService _sfu = new();
        private readonly FakeEventVideoBroadcaster _broadcaster = new();
        private VideoStreamReaper _reaper = default!;
        private VideoScenario _s = default!;
        private VideoStream _stream = default!;
        private DateTime _now;

        public VideoStreamReaperTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _reaper = new VideoStreamReaper(_repo, _sfu, _broadcaster, NullLogger<VideoStreamReaper>.Instance);
            _s = await VideoScenario.Create(_fx.Seed);
            _stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.JoinEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            // Every time-sensitive row is seeded relative to this one instant, which is also
            // the utcNow handed to the reaper: no test depends on the wall clock moving.
            _now = DateTime.UtcNow;
            await SetStartedAt(_now - TimeSpan.FromMinutes(10));
        }

        private Task SetStartedAt(DateTime startedAt) =>
            _fx.Seed.Execute($"update {S}.video_streams set started_at = @t where id = @s", ("t", startedAt), ("s", _stream.Id));

        public Task DisposeAsync() => Task.CompletedTask;

        private Task MakeStale(Guid userId, int seconds = 61) =>
            _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = @t where participant_id = @u",
                ("u", userId), ("t", _now - TimeSpan.FromSeconds(seconds)));

        private Task<string?> LeftReason(Guid userId) =>
            _fx.Seed.Scalar<string?>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", userId));

        [Fact]
        public async Task EvictsStaleParticipant_OnSfuAndInDb_AndBroadcasts()
        {
            await MakeStale(_s.AttendeeId);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Equal("reaped", await LeftReason(_s.AttendeeId));
            Assert.Equal($"Remove {_stream.RoomName} {_stream.Id}:{_s.AttendeeId}", Assert.Single(_sfu.Calls));
            Assert.Contains(_broadcaster.Events, e => e.Name == "participant_left" && e.Subject == _s.AttendeeId && e.Detail == "reaped");
        }

        [Fact]
        public async Task DoesNotEvictHeartbeatingParticipant()
        {
            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await LeftReason(_s.AttendeeId));
            Assert.Empty(_sfu.Calls);
        }

        [Fact]
        public async Task KeepsParticipantJustInsideTheTimeout()
        {
            await MakeStale(_s.AttendeeId, seconds: 59);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await LeftReason(_s.AttendeeId));
        }

        [Fact]
        public async Task EndsStream_WhenEveryoneIsStale_WithOneRoomDeleteAndNoSeparateKicks()
        {
            await MakeStale(_s.AttendeeId);
            await MakeStale(_s.OrganizerId);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            // Deleting the room disconnects everyone; kicking first would cost an extra operator session.
            Assert.Equal($"DeleteRoom {_stream.RoomName}", Assert.Single(_sfu.Calls));
            Assert.Contains(_broadcaster.Events, e => e.Name == "stream_ended" && e.Subject == _stream.Id);
        }

        [Fact]
        public async Task KeepsStream_WhenSomeoneIsStillHere()
        {
            await MakeStale(_s.AttendeeId);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.DoesNotContain(_sfu.Calls, c => c.StartsWith("DeleteRoom"));
        }

        [Fact]
        public async Task EndsHostlessStream_EvenWithAViewerStillLive()
        {
            await _repo.LeaveEventStream(_fx.Client, _s.OrganizerId, _s.EventId, "explicit", default);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Equal($"DeleteRoom {_stream.RoomName}", Assert.Single(_sfu.Calls));
            Assert.Equal("ended", await LeftReason(_s.AttendeeId));
        }

        [Fact]
        public async Task EndsStreamPastFourHours_EvenWhenOccupied()
        {
            await SetStartedAt(_now - TimeSpan.FromHours(4) - TimeSpan.FromMinutes(1));

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Equal("expired", await LeftReason(_s.AttendeeId));
            Assert.Equal("expired", await LeftReason(_s.OrganizerId));
            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Equal($"DeleteRoom {_stream.RoomName}", Assert.Single(_sfu.Calls));
        }

        [Fact]
        public async Task KeepsStreamJustInsideFourHours()
        {
            await SetStartedAt(_now - TimeSpan.FromHours(3) - TimeSpan.FromMinutes(59));

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Empty(_sfu.Calls);
        }

        [Fact]
        public async Task SfuReportsParticipantAlreadyGone_StillRecordsLeaveAndContinues()
        {
            await MakeStale(_s.AttendeeId);
            _sfu.FailRoomTeardown = room => new ParticipantNotConnectedException(room);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Equal("reaped", await LeftReason(_s.AttendeeId));
            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
        }

        [Fact]
        public async Task BroadcastFailureForOneParticipant_DoesNotSkipTheNext()
        {
            var second = await _s.AddAttendee(_fx.Seed, "second");
            await _repo.JoinEventStream(_fx.Client, second, _s.EventId, default);
            await MakeStale(_s.AttendeeId);
            await MakeStale(second);
            _broadcaster.ParticipantLeftFailure = new InvalidOperationException("broadcast down");

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Equal("reaped", await LeftReason(_s.AttendeeId));
            Assert.Equal("reaped", await LeftReason(second));
            Assert.Equal(2, _broadcaster.Events.Count(e => e.Name == "participant_left"));
            Assert.Contains(_sfu.Calls, c => c.StartsWith("Remove ") && c.Contains(_s.AttendeeId.ToString()) && c.Contains(second.ToString()));
        }

        [Fact]
        public async Task FailingRoomDeleteOnOneStream_DoesNotAbortTheNextStream_AndIsRetriedLater()
        {
            var other = await VideoScenario.Create(_fx.Seed);
            var otherStream = await _repo.StartEventStream(_fx.Client, other.OrganizerId, other.EventId, default);
            await _repo.JoinEventStream(_fx.Client, other.OrganizerId, other.EventId, default);
            await _fx.Seed.Execute($"update {S}.video_streams set started_at = @t where id = @s",
                ("t", _now - TimeSpan.FromMinutes(10)), ("s", otherStream.Id));
            await MakeStale(_s.OrganizerId);
            await MakeStale(_s.AttendeeId);
            await MakeStale(other.OrganizerId);
            _sfu.FailRoomTeardown = room => room == _stream.RoomName ? new InvalidOperationException("galene down") : null;

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            // The SFU delete failed, so that stream stays live in the DB; the other one ended.
            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, other.EventId, default));
            Assert.Contains(_broadcaster.Events, e => e.Name == "stream_ended" && e.Subject == otherStream.Id);

            _sfu.FailRoomTeardown = null;
            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
        }

        [Fact]
        public async Task SfuRemoveFailure_LeavesParticipantLive_AndLaterSweepSucceeds()
        {
            await MakeStale(_s.AttendeeId);
            _sfu.FailRoomTeardown = _ => new InvalidOperationException("galene down");

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await LeftReason(_s.AttendeeId));
            Assert.DoesNotContain(_broadcaster.Events, e => e.Name == "participant_left");

            _sfu.FailRoomTeardown = null;
            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Equal("reaped", await LeftReason(_s.AttendeeId));
        }

        [Fact]
        public async Task ExpiredStream_StaysLiveWhileSfuDeleteFails_ThenEnds()
        {
            await SetStartedAt(_now - TimeSpan.FromHours(5));
            _sfu.FailRoomTeardown = _ => new InvalidOperationException("galene down");

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Null(await LeftReason(_s.AttendeeId));

            _sfu.FailRoomTeardown = null;
            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Equal("expired", await LeftReason(_s.AttendeeId));
        }

        [Theory]
        [InlineData(59, true)]
        [InlineData(61, false)]
        public async Task FreshStreamGetsGraceBeforeEmptyOrHostlessRules(int ageSeconds, bool survives)
        {
            await SetStartedAt(_now - TimeSpan.FromSeconds(ageSeconds));
            await MakeStale(_s.OrganizerId);
            await MakeStale(_s.AttendeeId);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.Equal(survives, await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default) is not null);
        }

        [Fact]
        public async Task FreshHostlessStream_IsNotEnded()
        {
            await SetStartedAt(_now - TimeSpan.FromSeconds(30));
            await _repo.LeaveEventStream(_fx.Client, _s.OrganizerId, _s.EventId, "explicit", default);

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Empty(_sfu.Calls);
        }

        [Fact]
        public async Task StreamEndedConcurrently_IsBenign_NoBroadcast()
        {
            await MakeStale(_s.OrganizerId);
            await MakeStale(_s.AttendeeId);
            _sfu.BeforeDeleteRoom = () => _fx.Seed.Execute(
                $"update {S}.video_streams set ended_at = @t where id = @s", ("t", _now), ("s", _stream.Id));

            await _reaper.ReapOnceAsync(_fx.Client, _now, default);

            Assert.DoesNotContain(_broadcaster.Events, e => e.Name == "stream_ended");
        }

        [Fact]
        public async Task CancelledSweep_Throws_InsteadOfSwallowing()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _reaper.ReapOnceAsync(_fx.Client, _now, cts.Token));
        }

        [Fact]
        public async Task Purge_Uses90DayRetention()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = @t where participant_id = @u",
                ("u", _s.AttendeeId), ("t", _now - TimeSpan.FromDays(91)));
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = @t where participant_id = @u",
                ("u", _s.OrganizerId), ("t", _now - TimeSpan.FromDays(89)));

            Assert.Equal(1, await _reaper.PurgeAsync(_fx.Client, _now, default));
        }
    }

    public class VideoStreamReaperServiceTests
    {
        private sealed class CountingReaper : IVideoStreamReaper
        {
            public List<DateTime> Reaps { get; } = new();
            public List<DateTime> Purges { get; } = new();
            public bool FailReap { get; set; }

            public Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct)
            {
                Reaps.Add(utcNow);
                return FailReap ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
            }

            public Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct)
            {
                Purges.Add(utcNow);
                return Task.FromResult(0);
            }
        }

        private static (VideoStreamReaperService Service, CountingReaper Reaper) Build()
        {
            var reaper = new CountingReaper();
            var provider = new ServiceCollection().AddSingleton<IVideoStreamReaper>(reaper).BuildServiceProvider();
            var service = new VideoStreamReaperService(
                provider.GetRequiredService<IServiceScopeFactory>(), null!, TimeProvider.System,
                NullLogger<VideoStreamReaperService>.Instance);
            return (service, reaper);
        }

        [Fact]
        public async Task PurgesOncePerDay_NotEveryTick()
        {
            var (service, reaper) = Build();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            await service.TickAsync(t0, default);
            await service.TickAsync(t0.AddSeconds(30), default);
            await service.TickAsync(t0.AddHours(23), default);
            await service.TickAsync(t0.AddDays(1), default);

            Assert.Equal(4, reaper.Reaps.Count);
            Assert.Equal(new[] { t0, t0.AddDays(1) }, reaper.Purges);
        }

        [Fact]
        public async Task FailedSweep_IsSwallowedPerTick_ButCancellationIsNot()
        {
            var (service, reaper) = Build();
            reaper.FailReap = true;
            await service.TickAsync(DateTime.UtcNow, default);   // does not throw

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            reaper.FailReap = false;
            var cancelling = new VideoStreamReaperService(
                new ServiceCollection().AddSingleton<IVideoStreamReaper, CancellingReaper>().BuildServiceProvider()
                    .GetRequiredService<IServiceScopeFactory>(),
                null!, TimeProvider.System, NullLogger<VideoStreamReaperService>.Instance);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelling.TickAsync(DateTime.UtcNow, cts.Token));
        }

        private sealed class CancellingReaper : IVideoStreamReaper
        {
            public Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct) =>
                Task.FromCanceled(ct);

            public Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct) =>
                Task.FromResult(0);
        }
    }
}
