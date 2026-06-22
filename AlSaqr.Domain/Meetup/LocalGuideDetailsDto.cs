
using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    public class LocalGuideDetailsDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName("slug")]
        public string? Slug { get; set; }
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("citiesHosted")]
        public IDictionary<string, object>[]? CitiesHosted { get; set; }

        [JsonPropertyName("registeredAt")]
        public DateTime RegisteredAt { get; set; }

        [JsonPropertyName("distanceKm")]
        public decimal DistanceKm { get; set; }
        [JsonPropertyName("userInfo")]
        public LocalGuideUserInfoDto? UserInfo { get; set; }
    }

    /// <summary>
    /// Profile information for the user behind a local guide. This is a response DTO
    /// mapped from the AlSaqrUser entity — the Supabase BaseModel entity MUST NOT be
    /// serialized directly (its PrimaryKey dictionary is not serializable by
    /// System.Text.Json).
    /// </summary>
    public class LocalGuideUserInfoDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("bannerImage")]
        public string? BannerImage { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonPropertyName("countryOfOrigin")]
        public string? CountryOfOrigin { get; set; }

        [JsonPropertyName("maritalStatus")]
        public string? MaritalStatus { get; set; }

        [JsonPropertyName("religion")]
        public string? Religion { get; set; }

        [JsonPropertyName("preferredMadhab")]
        public string? PreferredMadhab { get; set; }

        [JsonPropertyName("frequentMasjid")]
        public string? FrequentMasjid { get; set; }

        [JsonPropertyName("hobbies")]
        public string[]? Hobbies { get; set; }

        [JsonPropertyName("favoriteQuranReciters")]
        public string[]? FavoriteQuranReciters { get; set; }

        [JsonPropertyName("favoriteIslamicScholars")]
        public string[]? FavoriteIslamicScholars { get; set; }

        [JsonPropertyName("islamicStudyTopics")]
        public string[]? IslamicStudyTopics { get; set; }

        [JsonPropertyName("isVerified")]
        public bool IsVerified { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}
