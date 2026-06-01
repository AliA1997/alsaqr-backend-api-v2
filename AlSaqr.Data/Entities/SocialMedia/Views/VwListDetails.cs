using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_list_details")]
    public class VwListDetails : BaseModel
    {
        [PrimaryKey("list_id", false)]
        public Guid ListId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("list_name")]
        public string ListName { get; set; } = string.Empty;

        [Column("list_description")]
        public string? ListDescription { get; set; }

        [Column("list_avatar")]
        public string? ListAvatar { get; set; }

        [Column("list_banner_image")]
        public string? ListBannerImage { get; set; }

        [Column("list_tags")]
        public string[]? ListTags { get; set; }

        [Column("list_created_at")]
        public DateTime ListCreatedAt { get; set; }

        [Column("list_updated_at")]
        public DateTime? ListUpdatedAt { get; set; }

        // Owner
        [Column("owner_username")]
        public string OwnerUsername { get; set; } = string.Empty;

        [Column("owner_avatar")]
        public string? OwnerAvatar { get; set; }

        // Item counts
        [Column("total_items")]
        public long TotalItems { get; set; }

        [Column("user_items")]
        public long UserItems { get; set; }

        [Column("post_items")]
        public long PostItems { get; set; }

        [Column("community_items")]
        public long CommunityItems { get; set; }

        [Column("discussion_items")]
        public long DiscussionItems { get; set; }

        [Column("message_items")]
        public long MessageItems { get; set; }

        // Latest saved item preview
        [Column("last_item_id")]
        public Guid? LastItemId { get; set; }

        [Column("last_item_type")]
        public string? LastItemType { get; set; }

        [Column("last_saved_at")]
        public DateTime? LastSavedAt { get; set; }
    }
}
