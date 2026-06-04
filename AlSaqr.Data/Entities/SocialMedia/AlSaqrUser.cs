
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("users")]
    public class AlSaqrUser : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("username")]
        public string Username { get; set; }
        [Column("avatar")]
        public string? Avatar { get; set; }
        [Column("email")]
        public string? Email { get; set; }

        [Column("banner_image")]
        public string? BannerImage { get; set; }

        [Column("bio")]
        public string? Bio { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("date_of_birth")]
        public DateTime? DateOfBirth { get; set; }

        [Column("country_of_origin")]
        public string? CountryOfOrigin { get; set; }

        [Column("marital_status")]
        public string? MaritalStatus { get; set; }

        [Column("religion")]
        public string? Religion { get; set; }

        [Column("preferred_madhab")]
        public string? PreferredMadhab { get; set; }

        [Column("frequent_masjid")]
        public string? FrequentMasjid { get; set; }

        [Column("hobbies")]
        public string[]? Hobbies { get; set; }

        [Column("favorite_quran_reciters")]
        public string[]? FavoriteQuranReciters { get; set; }

        [Column("favorite_islamic_scholars")]
        public string[]? FavoriteIslamicScholars { get; set; }

        [Column("islamic_study_topics")]
        public string[]? IslamicStudyTopics { get; set; }

        [Column("is_completed")]
        public bool IsCompleted { get; set; }

        [Column("is_verified")]
        public bool IsVerified { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

    }
}
