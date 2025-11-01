using Newtonsoft.Json;
namespace AlSaqr.Data.Entities
{
    public class Neo4jUserDto
    {
        
        [JsonProperty("lastName")]
        public string LastName { get; set; }

        [JsonProperty("frequentMasjid")]
        public string FrequentMasjid { get; set; }

        [JsonProperty("favoriteQuranReciters")]
        public List<string> FavoriteQuranReciters { get; set; }

        [JsonProperty("followingUsers")]
        public List<string> FollowingUsers { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("followedByUsers")]
        public List<string> FollowedByUsers { get; set; }

        [JsonProperty("preferredMadhab")]
        public string PreferredMadhab { get; set; }

        [JsonProperty("religion")]
        public string Religion { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("islamicStudyTopics")]
        public List<string> IslamicStudyTopics { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("hobbies")]
        public List<string> Hobbies { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("countryOfOrigin")]
        public string CountryOfOrigin { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("favoriteIslamicScholars")]
        public List<string> FavoriteIslamicScholars { get; set; }

        [JsonProperty("bgThumbnail")]
        public string BgThumbnail { get; set; }

        [JsonProperty("maritalStatus")]
        public string MaritalStatus { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("isCompleted")]
        public bool IsCompleted { get; set; }
    }
}
