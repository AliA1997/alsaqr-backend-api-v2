

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_list_item_details")]
    public class VwListItemDetails : BaseModel
    {
        [PrimaryKey("list_item_id", false)]
        public Guid ListItemId { get; set; }

        [Column("list_id")]
        public Guid ListId { get; set; }

        [Column("item_type")]
        public string ItemType { get; set; } = string.Empty;

        [Column("saved_at")]
        public DateTime SavedAt { get; set; }

        // Post
        [Column("post_id")]
        public Guid? PostId { get; set; }

        [Column("post_content")]
        public string? PostContent { get; set; }

        [Column("post_type")]
        public string? PostType { get; set; }

        [Column("post_tags")]
        public string[]? PostTags { get; set; }

        [Column("post_created_at")]
        public DateTime? PostCreatedAt { get; set; }

        [Column("post_username")]
        public string? PostUsername { get; set; }

        [Column("post_avatar")]
        public string? PostAvatar { get; set; }

        [Column("post_banner_image")]
        public string? PostBannerImage { get; set; }

        // Community
        [Column("community_id")]
        public Guid? CommunityId { get; set; }

        [Column("community_name")]
        public string? CommunityName { get; set; }

        [Column("community_description")]
        public string? CommunityDescription { get; set; }

        [Column("community_avatar")]
        public string? CommunityAvatar { get; set; }

        [Column("community_banner_image")]
        public string? CommunityBannerImage { get; set; }

        [Column("community_tags")]
        public string[]? CommunityTags { get; set; }

        [Column("community_created_at")]
        public DateTime? CommunityCreatedAt { get; set; }

        [Column("community_total_members")]
        public long CommunityTotalMembers { get; set; }

        [Column("community_founder_username")]
        public string? CommunityFounderUsername { get; set; }

        [Column("community_founder_avatar")]
        public string? CommunityFounderAvatar { get; set; }

        // Saved user
        [Column("saved_user_id")]
        public Guid? SavedUserId { get; set; }

        [Column("saved_user_username")]
        public string? SavedUserUsername { get; set; }

        [Column("saved_user_avatar")]
        public string? SavedUserAvatar { get; set; }

        [Column("saved_user_bio")]
        public string? SavedUserBio { get; set; }

        // Community discussion
        [Column("community_discussion_id")]
        public Guid? CommunityDiscussionId { get; set; }

        [Column("community_discussion_title")]
        public string? CommunityDiscussionTitle { get; set; }

        [Column("community_discussion_content")]
        public string? CommunityDiscussionContent { get; set; }

        [Column("community_discussion_created_at")]
        public DateTime? CommunityDiscussionCreatedAt { get; set; }

        [Column("community_discussion_username")]
        public string? CommunityDiscussionUsername { get; set; }

        [Column("community_discussion_avatar")]
        public string? CommunityDiscussionAvatar { get; set; }

        // Community discussion message
        [Column("community_discussion_message_id")]
        public Guid? CommunityDiscussionMessageId { get; set; }

        [Column("community_discussion_message_content")]
        public string? CommunityDiscussionMessageContent { get; set; }

        [Column("community_discussion_message_media")]
        public string? CommunityDiscussionMessageMedia { get; set; }

        [Column("community_discussion_message_created_at")]
        public DateTime? CommunityDiscussionMessageCreatedAt { get; set; }

        [Column("community_discussion_message_username")]
        public string? CommunityDiscussionMessageUsername { get; set; }

        [Column("community_discussion_message_avatar")]
        public string? CommunityDiscussionMessageAvatar { get; set; }
    }
}
