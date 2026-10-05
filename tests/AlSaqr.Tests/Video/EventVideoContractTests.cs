using System.Text.Json;
using Xunit;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Tests.Video
{
    public class EventVideoContractTests
    {
        private static HashSet<string> Keys(JsonElement element) =>
            element.EnumerateObject().Select(p => p.Name).ToHashSet();

        [Fact]
        public void JoinResult_SerializesToContractShape()
        {
            var dto = new JoinVideoStreamResultDto
            {
                Stream = new VideoStreamToDisplay { VideoStreamId = Guid.NewGuid() },
                Participants = { new VideoParticipantDto { Username = "a" } },
            };

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));
            var root = doc.RootElement;

            Assert.Equal(
                new HashSet<string> { "stream", "role", "wsUrl", "group", "token", "expiresAt", "canPublish", "participants" },
                Keys(root));
            Assert.Equal(
                new HashSet<string>
                {
                    "videoStreamId", "eventId", "eventName", "hostId", "hostUsername", "hostAvatar",
                    "startedAt", "endedAt", "participantCount", "isLive",
                },
                Keys(root.GetProperty("stream")));
            Assert.Equal(
                new HashSet<string> { "participantId", "username", "avatar", "role", "cameraEnabled", "muted" },
                Keys(root.GetProperty("participants")[0]));
        }

        [Fact]
        public void VideoToken_SerializesToContractShape()
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new VideoTokenDto()));

            Assert.Equal(
                new HashSet<string> { "role", "wsUrl", "group", "token", "expiresAt", "canPublish" },
                Keys(doc.RootElement));
        }

        [Fact]
        public void CameraStateForm_BindsFromCamelCaseValues()
        {
            var form = JsonSerializer.Deserialize<CameraStateForm>(
                "{\"cameraEnabled\":true,\"muted\":false}",
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.True(form!.CameraEnabled);
            Assert.False(form.Muted);
        }
    }
}
