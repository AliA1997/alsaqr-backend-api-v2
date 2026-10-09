using System.Net;
using System.Text.Json;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.Extensions.Options;

namespace AlSaqr.Tests.Video
{
    public class EventVideoBroadcasterTests
    {
        private const string ServiceRole = "service-role-secret-value";

        private readonly RecordingHandler _handler = new();
        private readonly EventVideoBroadcaster _broadcaster;

        public EventVideoBroadcasterTests()
        {
            var client = new SupabaseBroadcastClient(
                new HttpClient(_handler),
                Options.Create(new SupabaseRealtimeConfig { Url = "https://proj.supabase.test/", ServiceRoleSecret = ServiceRole }));
            _broadcaster = new EventVideoBroadcaster(client);
        }

        private JsonElement OnlyMessage()
        {
            var request = Assert.Single(_handler.Requests);
            using var doc = JsonDocument.Parse(request.Body);
            return doc.RootElement.GetProperty("messages")[0].Clone();
        }

        [Fact]
        public async Task StreamStarted_PostsToEventVideoTopic_WithServiceRoleHeaders()
        {
            var eventId = Guid.NewGuid();
            var streamId = Guid.NewGuid();

            await _broadcaster.StreamStartedAsync(eventId, streamId, DateTime.UtcNow);

            var request = Assert.Single(_handler.Requests);
            Assert.Equal("https://proj.supabase.test/realtime/v1/api/broadcast", request.Url);
            Assert.Equal(ServiceRole, request.Headers["apikey"]);
            Assert.Equal($"Bearer {ServiceRole}", request.Headers["Authorization"]);

            var message = OnlyMessage();
            Assert.Equal($"event_video:{eventId}", message.GetProperty("topic").GetString());
            Assert.Equal("stream_started", message.GetProperty("event").GetString());
            Assert.Equal(streamId, message.GetProperty("payload").GetProperty("videoStreamId").GetGuid());
            Assert.Equal(eventId, message.GetProperty("payload").GetProperty("eventId").GetGuid());
            Assert.True(message.GetProperty("payload").TryGetProperty("startedAt", out _));
            Assert.True(message.GetProperty("private").GetBoolean());
            Assert.Contains("\"private\":true", request.Body);
        }

        [Fact]
        public async Task StreamEnded_PostsEndedPayload()
        {
            var eventId = Guid.NewGuid();
            var streamId = Guid.NewGuid();

            await _broadcaster.StreamEndedAsync(eventId, streamId, DateTime.UtcNow);

            var message = OnlyMessage();
            Assert.Equal($"event_video:{eventId}", message.GetProperty("topic").GetString());
            Assert.Equal("stream_ended", message.GetProperty("event").GetString());
            Assert.Equal(streamId, message.GetProperty("payload").GetProperty("videoStreamId").GetGuid());
            Assert.True(message.GetProperty("payload").TryGetProperty("endedAt", out _));
        }

        [Fact]
        public async Task MissingConfig_ThrowsClearError()
        {
            var client = new SupabaseBroadcastClient(
                new HttpClient(_handler),
                Options.Create(new SupabaseRealtimeConfig { Url = "", ServiceRoleSecret = "" }));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.BroadcastAsync("t", "e", new { }));
            Assert.Empty(_handler.Requests);
        }

        [Fact]
        public async Task ParticipantJoined_CarriesFullRosterEntry()
        {
            var participant = new AlSaqr.Domain.Meetup.EventVideo.VideoParticipantDto
            {
                ParticipantId = Guid.NewGuid(),
                Username = "amina",
                Avatar = "https://cdn.test/a.png",
                Role = "viewer",
                Muted = true,
            };

            await _broadcaster.ParticipantJoinedAsync(Guid.NewGuid(), participant);

            var message = OnlyMessage();
            Assert.Equal("participant_joined", message.GetProperty("event").GetString());
            var payload = message.GetProperty("payload");
            Assert.Equal(participant.ParticipantId, payload.GetProperty("participantId").GetGuid());
            Assert.Equal("amina", payload.GetProperty("username").GetString());
            Assert.Equal("viewer", payload.GetProperty("role").GetString());
            var names = payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "avatar", "cameraEnabled", "muted", "participantId", "role", "username" }, names);
        }

        [Fact]
        public async Task ParticipantLeft_CarriesReason()
        {
            var participant = Guid.NewGuid();

            await _broadcaster.ParticipantLeftAsync(Guid.NewGuid(), participant, "reaped");

            var message = OnlyMessage();
            Assert.Equal("participant_left", message.GetProperty("event").GetString());
            Assert.Equal("reaped", message.GetProperty("payload").GetProperty("reason").GetString());
            Assert.Equal(participant, message.GetProperty("payload").GetProperty("participantId").GetGuid());
        }

        [Fact]
        public async Task FailedBroadcast_Throws_WithoutLeakingServiceRoleKey()
        {
            _handler.Status = HttpStatusCode.Unauthorized;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _broadcaster.RoleChangedAsync(Guid.NewGuid(), Guid.NewGuid(), "presenter"));

            Assert.Contains("role_changed", ex.Message);
            Assert.DoesNotContain(ServiceRole, ex.Message);
        }
    }
}
