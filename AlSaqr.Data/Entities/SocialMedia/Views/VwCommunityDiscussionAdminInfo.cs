using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_community_discussion_admin_info")]
    public class VwCommunityDiscussionAdminInfo : BaseModel
    {
        // Views have no real primary key; mark the discussion id as the key for
        // read operations and exclude it from inserts (shouldInsert: false).
        [PrimaryKey("discussion_id", false)]
        public Guid DiscussionId { get; set; }

        [Column("community_id")]
        public Guid CommunityId { get; set; }

        [Column("creator_id")]
        public Guid CreatorId { get; set; }

        [Column("discussion_title")]
        public string? DiscussionTitle { get; set; }

        [Column("discussion_content")]
        public string? DiscussionContent { get; set; }

        [Column("discussion_created_at")]
        public DateTime DiscussionCreatedAt { get; set; }

        [Column("founder_username")]
        public string? FounderUsername { get; set; }

        [Column("founder_avatar")]
        public string? FounderAvatar { get; set; }

        [Column("founder_first_name")]
        public string? FounderFirstName { get; set; }

        [Column("founder_last_name")]
        public string? FounderLastName { get; set; }

        [Column("invited_count")]
        public int InvitedCount { get; set; }

        [Column("joined_count")]
        public int JoinedCount { get; set; }

        [Column("requested_count")]
        public int RequestedCount { get; set; }
    }
}
