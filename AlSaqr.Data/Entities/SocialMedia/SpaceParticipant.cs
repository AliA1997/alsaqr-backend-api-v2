using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    /// <summary>
    /// A participant of an audio space (specs/audio-spaces.md). Role is
    /// backend-authoritative: publish is only granted when the persisted role is
    /// host or speaker at the time of the publish call.
    /// </summary>
    [Table("space_participants")]
    public class SpaceParticipant : BaseModel
    {
        public const string RoleHost = "host";
        public const string RoleSpeaker = "speaker";
        public const string RoleListener = "listener";

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("space_id")]
        public Guid SpaceId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>"host" | "speaker" | "listener"</summary>
        [Column("role")]
        public string Role { get; set; } = RoleListener;

        [Column("muted")]
        public bool Muted { get; set; }

        [Column("hand_raised")]
        public bool HandRaised { get; set; }

        /// <summary>Cloudflare session id — set while publishing.</summary>
        [Column("sfu_session_id")]
        public string? SfuSessionId { get; set; }

        /// <summary>Cloudflare track name — set while publishing.</summary>
        [Column("track_name")]
        public string? TrackName { get; set; }

        /// <summary>
        /// Media-line id of the published track; needed to close the track on the
        /// SFU during leave / demote / end / reap.
        /// </summary>
        [Column("sfu_mid")]
        public string? SfuMid { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the participant is present.</summary>
        [Column("left_at")]
        public DateTime? LeftAt { get; set; }

        /// <summary>
        /// Refreshed by any authenticated space call from the participant; drives
        /// the reaper that closes SFU tracks for silently-dead participants.
        /// </summary>
        [Column("last_seen_at")]
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
