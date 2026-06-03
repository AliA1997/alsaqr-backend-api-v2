using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_session_user")]
    public class VwSessionUser : BaseModel
    {
        // Core identity
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Profile fields
        [Column("country_of_origin")]
        public string? CountryOfOrigin { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("bio")]
        public string? Bio { get; set; }

        [Column("bg_thumbnail")]
        public string? BgThumbnail { get; set; }

        [Column("avatar")]
        public string? Avatar { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("geo_id")]
        public string? GeoId { get; set; }

        [Column("marital_status")]
        public string? MaritalStatus { get; set; }

        [Column("religion")]
        public string? Religion { get; set; }

        [Column("preferred_madhab")]
        public string? PreferredMadhab { get; set; }

        [Column("frequent_masjid")]
        public string? FrequentMasjid { get; set; }

        [Column("verified")]
        public bool? Verified { get; set; }

        [Column("is_completed")]
        public bool? IsCompleted { get; set; }

        // Islamic / interest arrays (text[])
        [Column("hobbies")]
        public string[]? Hobbies { get; set; } = Array.Empty<string>();

        [Column("favorite_quran_reciters")]
        public string[]? FavoriteQuranReciters { get; set; } = Array.Empty<string>();

        [Column("favorite_islamic_scholars")]
        public string[]? FavoriteIslamicScholars { get; set; } = Array.Empty<string>();

        [Column("islamic_study_topics")]
        public string[]? IslamicStudyTopics { get; set; } = Array.Empty<string>();

        // Post-relation id arrays (uuid[])
        [Column("bookmarks")]
        public Guid[] Bookmarks { get; set; } = Array.Empty<Guid>();

        [Column("reposts")]
        public Guid[] Reposts { get; set; } = Array.Empty<Guid>();

        [Column("liked_posts")]
        public Guid[] LikedPosts { get; set; } = Array.Empty<Guid>();

        // Following / Followers (json → string, deserialized in mapper)
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