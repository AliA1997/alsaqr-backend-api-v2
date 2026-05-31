using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_user_profile_info")]
    public class VwUserProfileInfo : BaseModel
    {
        [PrimaryKey("user_id", false)]
        public Guid UserId { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("avatar")]
        public string? Avatar { get; set; }

        [Column("bio")]
        public string? Bio { get; set; }

        [Column("user_created_at")]
        public DateTimeOffset UserCreatedAt { get; set; }

        [Column("user_updated_at")]
        public DateTimeOffset? UserUpdatedAt { get; set; }

        // Bookmarks
        [Column("bookmark_ids")]
        public Guid[]? BookmarkIds { get; set; }

        [Column("bookmark_count")]
        public long BookmarkCount { get; set; }

        // Following / Followers (JSONB → string, deserialized in mapper)
        [Column("following")]
        public string Following { get; set; } = "[]";

        [Column("following_count")]
        public long FollowingCount { get; set; }

        [Column("followers")]
        public string Followers { get; set; } = "[]";

        [Column("follower_count")]
        public long FollowerCount { get; set; }
    }

}
