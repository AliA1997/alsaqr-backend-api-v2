using System.Text.Json.Serialization;

namespace AlSaqr.Domain.SocialMedia
{
    /// <summary>
    /// DTOs for ephemeral audio spaces (specs/audio-spaces.md). Field names are a
    /// frozen contract with the React client — they must serialize to exactly the
    /// TypeScript shapes in the spec.
    /// </summary>
    public static class Spaces
    {
        // ----- Response DTOs -----

        public class SpaceToDisplay
        {
            [JsonPropertyName("spaceId")]
            public Guid SpaceId { get; set; }

            /// <summary>"community" | "community-discussion"</summary>
            [JsonPropertyName("kind")]
            public string Kind { get; set; } = string.Empty;

            [JsonPropertyName("communityId")]
            public Guid CommunityId { get; set; }

            [JsonPropertyName("communityDiscussionId")]
            public Guid? CommunityDiscussionId { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

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

        public class SpaceParticipantDto
        {
            [JsonPropertyName("userId")]
            public Guid UserId { get; set; }

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }

            /// <summary>"host" | "speaker" | "listener"</summary>
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("muted")]
            public bool Muted { get; set; }

            [JsonPropertyName("handRaised")]
            public bool HandRaised { get; set; }

            /// <summary>Set for publishing speakers.</summary>
            [JsonPropertyName("sfuSessionId")]
            public string? SfuSessionId { get; set; }

            /// <summary>Set for publishing speakers.</summary>
            [JsonPropertyName("trackName")]
            public string? TrackName { get; set; }
        }

        public class JoinSpaceResultDto
        {
            [JsonPropertyName("space")]
            public SpaceToDisplay Space { get; set; } = new();

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("participants")]
            public List<SpaceParticipantDto> Participants { get; set; } = new();

            /// <summary>Currently publishing, so the joiner can pull each track.</summary>
            [JsonPropertyName("speakers")]
            public List<SpaceParticipantDto> Speakers { get; set; } = new();
        }

        /// <summary>WebRTC session description (RTCSessionDescriptionInit shape).</summary>
        public class SessionDescriptionDto
        {
            /// <summary>"offer" | "answer"</summary>
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("sdp")]
            public string Sdp { get; set; } = string.Empty;
        }

        /// <summary>Publish: the client OFFERS, the SFU ANSWERS.</summary>
        public class PublishResultDto
        {
            [JsonPropertyName("sessionId")]
            public string SessionId { get; set; } = string.Empty;

            [JsonPropertyName("trackName")]
            public string TrackName { get; set; } = string.Empty;

            [JsonPropertyName("answer")]
            public SessionDescriptionDto Answer { get; set; } = new();
        }

        /// <summary>Subscribe: the SFU OFFERS, the client answers via renegotiate.</summary>
        public class SubscribeResultDto
        {
            [JsonPropertyName("sessionId")]
            public string SessionId { get; set; } = string.Empty;

            [JsonPropertyName("offer")]
            public SessionDescriptionDto Offer { get; set; } = new();
        }

        // ----- Request forms (arrive wrapped in AlSaqrUpsertRequest<T> { values: ... }) -----

        public class StartSpaceForm
        {
            public string Title { get; set; } = string.Empty;
        }

        public class PublishForm
        {
            public string Sdp { get; set; } = string.Empty;
            public string Mid { get; set; } = string.Empty;
        }

        public class SubscribeForm
        {
            public string PubSessionId { get; set; } = string.Empty;
            public string TrackName { get; set; } = string.Empty;
        }

        public class RenegotiateForm
        {
            public string SessionId { get; set; } = string.Empty;
            public string Sdp { get; set; } = string.Empty;
        }

        public class RaiseHandForm
        {
            public bool Raised { get; set; }
        }
    }
}
