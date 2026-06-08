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
        [Column("banner_image")]
        public string? BannerImage { get; set; }
        [Column("bio")]
        public string? Bio { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }
        [Column("last_name")]
        public string LastName { get; set; }
        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("user_created_at")]
        public DateTime UserCreatedAt { get; set; }

        [Column("user_updated_at")]
        public DateTime? UserUpdatedAt { get; set; }

        // Bookmarks
        [Column("bookmark_ids")]
        public Guid[]? BookmarkIds { get; set; }

        [Column("bookmark_count")]
        public long BookmarkCount { get; set; }

        // Following / Followers (JSONB → string, deserialized in mapper)
        [Column("following")]
        public IDictionary<string, object>[]? Following { get; set; }

        [Column("following_count")]
        public long FollowingCount { get; set; }

        [Column("followers")]
        public IDictionary<string, object>[]? Followers { get; set; }

        [Column("follower_count")]
        public long FollowerCount { get; set; }
    }

}
