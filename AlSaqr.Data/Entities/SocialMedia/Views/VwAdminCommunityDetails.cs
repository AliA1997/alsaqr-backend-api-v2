using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_admin_community_details")]

    public class VwAdminCommunityDetails : BaseModel
    {
        [Column("community_id")]
        public Guid CommunityId { get; set; }

        [Column("community_name")]
        public string CommunityName { get; set; } = string.Empty;

        [Column("community_description")]
        public string? CommunityDescription { get; set; }

        [Column("community_avatar")]
        public string? CommunityAvatar { get; set; }

        [Column("community_banner_image")]
        public string? CommunityBannerImage { get; set; }

        [Column("community_tags")]
        public string[]? CommunityTags { get; set; }

        [Column("community_created_at")]
        public DateTime CommunityCreatedAt { get; set; }

        [Column("community_updated_at")]
        public DateTime? CommunityUpdatedAt { get; set; }

        // Founder
        [Column("founder_id")]
        public Guid FounderId { get; set; }

        [Column("founder_username")]
        public string FounderUsername { get; set; } = string.Empty;

        [Column("founder_avatar")]
        public string? FounderAvatar { get; set; }

        [Column("relationship_type")]
        public string? RelationshipType { get; set; }

        // Counts
        [Column("total_members")]
        public long? TotalMembers { get; set; } = 0;

        [Column("invited_count")]
        public long? InvitedCount { get; set; } = 0;

        [Column("joined_count")]
        public long? JoinedCount { get; set; } = 0;

        [Column("moderator_counts")]
        public long? ModeratorCount { get; set; } = 0;

        [Column("invite_requested_users")]
        public IDictionary<string, object>[]? InviteRequestedUsers { get; set; }



    }
}
