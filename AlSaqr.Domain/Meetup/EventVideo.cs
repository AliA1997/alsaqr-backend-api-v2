using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// DTOs for online-event video streaming
    /// (specs/video-streaming-online-events.md). Field names are a contract with
    /// the client and must serialize to exactly the documented TypeScript shapes.
    /// </summary>
    public static class EventVideo
    {
        // ----- Response DTOs -----

        public class VideoStreamToDisplay
        {
            [JsonPropertyName("videoStreamId")]
            public Guid VideoStreamId { get; set; }

            [JsonPropertyName("eventId")]
            public Guid EventId { get; set; }

            [JsonPropertyName("eventName")]
            public string? EventName { get; set; }

            [JsonPropertyName("hostId")]
            public Guid HostId { get; set; }

            [JsonPropertyName("hostUsername")]
            public string? HostUsername { get; set; }

            [JsonPropertyName("hostAvatar")]
            public string? HostAvatar { get; set; }

            [JsonPropertyName("startedAt")]
            public DateTime StartedAt { get; set; }

            [JsonPropertyName("endedAt")]
            public DateTime? EndedAt { get; set; }

            [JsonPropertyName("participantCount")]
            public int ParticipantCount { get; set; }

            [JsonPropertyName("isLive")]
            public bool IsLive { get; set; }
        }

        public class VideoParticipantDto
        {
            [JsonPropertyName("participantId")]
            public Guid ParticipantId { get; set; }

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }

            /// <summary>"host" | "presenter" | "viewer"</summary>
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("cameraEnabled")]
            public bool CameraEnabled { get; set; }

            [JsonPropertyName("muted")]
            public bool Muted { get; set; }
        }

        /// <summary>
        /// The connect payload: everything the browser needs to reach the SFU, and
        /// nothing more. wsUrl, group and token are the ONLY SFU values that leave the API.
        /// </summary>
        public class JoinVideoStreamResultDto
        {
            [JsonPropertyName("stream")]
            public VideoStreamToDisplay Stream { get; set; } = new();

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("wsUrl")]
            public string WsUrl { get; set; } = string.Empty;

            /// <summary>Galène group (room) name the client joins with the token.</summary>
            [JsonPropertyName("group")]
            public string Group { get; set; } = string.Empty;

            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("expiresAt")]
            public DateTime ExpiresAt { get; set; }

            [JsonPropertyName("canPublish")]
            public bool CanPublish { get; set; }

            [JsonPropertyName("participants")]
            public List<VideoParticipantDto> Participants { get; set; } = new();
        }

        /// <summary>
        /// A fresh room token for a participant who is already live: used when the
        /// first connect is slow or a reconnect outlasts the 90 s token. Carries no
        /// roster and triggers no broadcast.
        /// </summary>
        public class VideoTokenDto
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("wsUrl")]
            public string WsUrl { get; set; } = string.Empty;

            [JsonPropertyName("group")]
            public string Group { get; set; } = string.Empty;

            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("expiresAt")]
            public DateTime ExpiresAt { get; set; }

            [JsonPropertyName("canPublish")]
            public bool CanPublish { get; set; }
        }

        // ----- Request forms (wrapped in AlSaqrUpsertRequest<T> { values: ... }) -----

        public class CameraStateForm
        {
            public bool CameraEnabled { get; set; }
            public bool Muted { get; set; }
        }
    }
}
