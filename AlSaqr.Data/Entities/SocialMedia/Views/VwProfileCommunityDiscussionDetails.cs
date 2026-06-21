using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_profile_community_discussion_details")]
    public class VwProfileCommunityDiscussionDetails : BaseModel
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }

        [Column("discussion_id")]
        public Guid DiscussionId { get; set; }

        [Column("community_id")]
        public Guid CommunityId { get; set; }

        [Column("discussion_title")]
        public string DiscussionTitle { get; set; } = string.Empty;

        [Column("discussion_content")]
        public string DiscussionContent { get; set; } = string.Empty;

        [Column("discussion_created_at")]
        public DateTime DiscussionCreatedAt { get; set; }

        [Column("discussion_updated_at")]
        public DateTime? DiscussionUpdatedAt { get; set; }

        // Creator
        [Column("creator_id")]
        public Guid CreatorId { get; set; }

        [Column("creator_username")]
        public string CreatorUsername { get; set; } = string.Empty;

        [Column("creator_avatar")]
        public string? CreatorAvatar { get; set; }

        [Column("relationship_type")]
        public string? RelationshipType { get; set; }

        // Aggregated member lists (json → string; deserialize downstream)
        [Column("joined_users")]
        public IDictionary<string , object>[]? JoinedUsers { get; set; }

        [Column("moderator_users")]
        public IDictionary<string, object>[]? ModeratorUsers { get; set; }
        [Column("invited_users")]
        public IDictionary<string, object>[]? InvitedUsers { get; set; }

        [Column("requested_users")]
        public IDictionary<string, object>[]? RequestedUsers { get; set; }

        // Counts
        [Column("member_count")]
        public long MemberCount { get; set; }

        [Column("moderator_count")]
        public long ModeratorCount { get; set; }
        [Column("invited_count")]
        public long InvitedCount { get; set; }

        [Column("requested_count")]
        public long RequestedCount { get; set; }

        [Column("total_member_count")]
        public long TotalMemberCount { get; set; }

        // Latest message preview
        [Column("last_message_id")]
        public Guid? LastMessageId { get; set; }

        [Column("last_message_creator_id")]
        public Guid? LastMessageCreatorId { get; set; }

        [Column("last_message_content")]
        public string? LastMessageContent { get; set; }

        [Column("last_message_at")]
        public DateTime? LastMessageAt { get; set; }
    }
}
