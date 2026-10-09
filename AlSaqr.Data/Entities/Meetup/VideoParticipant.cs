using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// A participant of an online event's video stream
    /// (specs/video-streaming-online-events.md). Role is backend-authoritative:
    /// publish rights are baked into the signed token AND revocable mid-session via
    /// UpdateParticipant, so a client can neither grant nor keep a camera on its
    /// own. LeftAt == null is the "in the live" indicator.
    /// </summary>
    [Table("video_participants")]
    public class VideoParticipant : BaseModel
    {
        public const string RoleHost = "host";
        public const string RolePresenter = "presenter";
        public const string RoleViewer = "viewer";

        public const string LeftExplicit = "explicit";
        public const string LeftReaped = "reaped";
        public const string LeftEnded = "ended";
        public const string LeftExpired = "expired";
        /// <summary>Removed from the event. Never restores a role on rejoin.</summary>
        public const string LeftKicked = "kicked";

        /// <summary>
        /// The SFU username for a user on a stream. Stream-scoped like the room name,
        /// so a token minted for an earlier stream cannot be replayed (spec �4).
        /// </summary>
        public static string SfuIdentityFor(Guid videoStreamId, Guid userId) => $"{videoStreamId}:{userId}";

        [PrimaryKey("id", true)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("event_id")]
        public Guid EventId { get; set; }

        /// <summary>The joining user (see the design note in the spec).</summary>
        [Column("participant_id")]
        public Guid ParticipantId { get; set; }

        [Column("video_stream_id")]
        public Guid VideoStreamId { get; set; }

        /// <summary>"host" | "presenter" | "viewer"</summary>
        [Column("role")]
        public string Role { get; set; } = RoleViewer;

        [Column("camera_enabled")]
        public bool CameraEnabled { get; set; }

        [Column("muted")]
        public bool Muted { get; set; } = true;

        /// <summary>LiveKit participant identity — set on join, cleared on leave.</summary>
        [Column("sfu_identity")]
        public string? SfuIdentity { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the participant is in the live stream.</summary>
        [Column("left_at")]
        public DateTime? LeftAt { get; set; }

        /// <summary>
        /// Why the participant left. Set together with LeftAt. Distinguishing
        /// "reaped" from "explicit" is what makes the involuntary-disconnect
        /// guardrail a SQL query instead of a guess (§10).
        /// </summary>
        [Column("left_reason")]
        public string? LeftReason { get; set; }

        /// <summary>
        /// Refreshed by any authenticated stream call from the participant; drives
        /// the reaper that disconnects silently-dead participants (C9).
        /// </summary>
        [Column("last_seen_at")]
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
