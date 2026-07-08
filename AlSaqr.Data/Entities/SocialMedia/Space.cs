using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    /// <summary>
    /// An ephemeral audio space (specs/audio-spaces.md). Only started_at/ended_at
    /// metadata is ever persisted — no audio artifact exists in any store.
    /// </summary>
    [Table("spaces")]
    public class Space : BaseModel
    {
        public const string KindCommunity = "community";
        public const string KindCommunityDiscussion = "community-discussion";

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>"community" | "community-discussion"</summary>
        [Column("kind")]
        public string Kind { get; set; } = KindCommunity;

        [Column("community_id")]
        public Guid CommunityId { get; set; }

        /// <summary>Set only for community-discussion spaces.</summary>
        [Column("community_discussion_id")]
        public Guid? CommunityDiscussionId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("host_id")]
        public Guid HostId { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the space is live.</summary>
        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }
    }
}
