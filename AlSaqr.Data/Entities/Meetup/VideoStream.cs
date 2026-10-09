using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// An ephemeral video stream for an online event
    /// (specs/video-streaming-online-events.md). Only started_at/ended_at metadata
    /// is ever persisted — no video artifact exists in any store.
    /// </summary>
    [Table("video_streams")]
    public class VideoStream : BaseModel
    {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("event_id")]
        public Guid EventId { get; set; }

        /// <summary>
        /// LiveKit room key. STREAM-scoped ("stream-{id}"), never event-scoped: a
        /// reused room name would let a token minted for an earlier stream of the
        /// same event be replayed into a later one (§4). Always read this column —
        /// never recompute the name at a call site.
        /// </summary>
        [Column("room_name")]
        public string RoomName { get; set; } = string.Empty;

        [Column("host_id")]
        public Guid HostId { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the stream is live.</summary>
        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }

        /// <summary>The room name for a stream id. Used ONLY when creating a stream.</summary>
        public static string RoomNameFor(Guid videoStreamId) => $"stream-{videoStreamId}";
    }
}
