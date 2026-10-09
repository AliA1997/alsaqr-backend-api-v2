using AlSaqr.Infrastructure.Video;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// Records SFU calls; several tests assert a call did NOT happen. Like the real
    /// HTTP-backed service, a call made with a cancelled token throws before it is recorded.
    /// </summary>
    public sealed class FakeVideoSfuService : IVideoSfuService
    {
        public List<string> Calls { get; } = new();

        public List<(string Room, string Identity, bool CanPublish)> MintedTokens { get; } = new();

        public bool FailCreateRoom { get; set; }

        /// <summary>Thrown by RemoveParticipantsAsync/DeleteRoomAsync after the call is recorded.</summary>
        public Func<string, Exception?>? FailRoomTeardown { get; set; }

        public Task CreateRoomAsync(string roomName, int maxParticipants, DateTime notAfterUtc, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add($"CreateRoom {roomName} max={maxParticipants}");
            if (FailCreateRoom)
                throw new InvalidOperationException("Galène admin PUT failed with status 503.");
            return Task.CompletedTask;
        }

        public SfuJoinToken MintJoinToken(string roomName, string identity, bool canPublish)
        {
            MintedTokens.Add((roomName, identity, canPublish));
            return new SfuJoinToken($"token-for-{identity}", "ws://galene.test/ws", roomName, DateTime.UtcNow.AddSeconds(90));
        }

        /// <summary>Thrown by SetPublishAsync after the call is recorded.</summary>
        public Func<string, Exception?>? FailSetPublish { get; set; }

        public Task SetPublishAsync(string roomName, string identity, bool canPublish, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add($"SetPublish {roomName} {identity} {canPublish}");
            var failure = FailSetPublish?.Invoke(roomName);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }

        /// <summary>Runs inside RemoveParticipantsAsync, to simulate something happening concurrently.</summary>
        public Func<Task>? BeforeRemove { get; set; }

        public async Task RemoveParticipantsAsync(string roomName, IReadOnlyCollection<string> identities, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add($"Remove {roomName} {string.Join(",", identities)}");
            if (BeforeRemove is not null)
                await BeforeRemove();
            await Fail(roomName);
        }

        /// <summary>Runs inside DeleteRoomAsync, to simulate something happening concurrently.</summary>
        public Func<Task>? BeforeDeleteRoom { get; set; }

        public async Task DeleteRoomAsync(string roomName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add($"DeleteRoom {roomName}");
            if (BeforeDeleteRoom is not null)
                await BeforeDeleteRoom();
            await Fail(roomName);
        }

        private Task Fail(string roomName)
        {
            var failure = FailRoomTeardown?.Invoke(roomName);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    public sealed class FakeEventVideoBroadcaster : IEventVideoBroadcaster
    {
        public List<(string Name, Guid EventId, Guid? Subject, string? Detail)> Events { get; } = new();

        /// <summary>When set, ParticipantLeftAsync records the event then throws this.</summary>
        public Exception? ParticipantLeftFailure { get; set; }

        public Task StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, CancellationToken ct = default) =>
            Record("stream_started", eventId, videoStreamId, null);

        public Task StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, CancellationToken ct = default) =>
            Record("stream_ended", eventId, videoStreamId, null);

        public Task ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, CancellationToken ct = default) =>
            Record("participant_joined", eventId, participant.ParticipantId, participant.Username);

        public async Task ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, CancellationToken ct = default)
        {
            await Record("participant_left", eventId, participantId, reason);
            if (ParticipantLeftFailure is not null)
                throw ParticipantLeftFailure;
        }

        public Task RoleChangedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default) =>
            Record("role_changed", eventId, participantId, role);

        private Task Record(string name, Guid eventId, Guid? subject, string? detail)
        {
            Events.Add((name, eventId, subject, detail));
            return Task.CompletedTask;
        }
    }
}
