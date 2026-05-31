using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class User
    {
        public class CheckUserResponse
        {
            [JsonPropertyName("user")]
            public IDictionary<string, object>? User { get; set; }

            [JsonPropertyName("bookmarks")]
            //public IEnumerable<IDictionary<string, object>>? Bookmarks { get; set; }
            public List<IReadOnlyDictionary<string, object>>? Bookmarks { get; set; }


            [JsonPropertyName("reposts")]
            //public IEnumerable<IDictionary<string, object>>? Reposts { get; set; }
            public List<IReadOnlyDictionary<string, object>>? Reposts { get; set; }

            

            [JsonPropertyName("likedPosts")]
            //public IEnumerable<IDictionary<string, object>>? LikedPosts { get; set; }
            public List<IReadOnlyDictionary<string, object>>? LikedPosts { get; set; }


            public long? Total { get; set; }

        }


        public class GetUserResponse
        {
            [JsonProperty("user")]
            public Common.User User { get; set; }
            
        }
        public class CreateInitialUserDto: UpdateUserDto
        {
            public DateTime CreatedAt { get; set; }
            public string Email { get; set; }
            public string? Phone { get; set; }
            public object Avatar { get; set; }
            public DateTime? DateOfBirth { get; set; }
            
        }
        public class UpdateUserDto
        {
            public Guid Id { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Avatar { get; set; }
            public string? BgThumbnail { get; set; }
            public DateTime DateOfBirth { get; set; }
            public string? Username { get; set; }
            public string? Bio { get; set; }
            public string? Religion { get; set; }
            public string? MaritalStatus { get; set; }
            public string[] Hobbies { get; set; }
            public string? CountryOfOrigin { get; set; }
            public string? PreferredMadhab { get; set; }
            public string? FrequentMasjid { get; set; }
            public string[]? FavoriteQuranReciters { get; set; }
            public string[]? FavoriteIslamicScholars { get; set; }
            public string[]? IslamicStudyTopics { get; set; }
        }

        public class FollowUserFormDto
        {
            public Guid UserToFollowId { get; set; }
        }

        public class UnFollowUserFormDto
        {
            public Guid UserToUnFollowId { get; set; }
        }

        public class UserRegisterFormDto
        {
            public string[] FollowingUsers { get; set; }
            public string? Bio { get; set; }
            public string? Username { get; set; }
            public string? Avatar { get; set; }
            public string? BgThumbnail { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string? MaritalStatus { get; set; }
            public string[]? Hobbies { get; set; }
            public string? Religion { get; set; }
            public string? CountryOfOrigin { get; set; }
        }

        public class UserProfilePostsResponse
        {
            [JsonPropertyName("userPosts")]
            public dynamic UserPosts { get; set; }

            [JsonPropertyName("bookmarkedPosts")]
            public dynamic BookmarkedPosts { get; set; }
            
            [JsonPropertyName("likedPosts")]
            public dynamic LikedPosts { get; set; }

            [JsonPropertyName("repostedPosts")]
            public dynamic RepostedPosts { get; set; }

            [JsonPropertyName("repliedPosts")]
            public dynamic RepliedPosts { get; set; }

            [JsonPropertyName("success")]
            public bool Success { get; set; }

        }
        public sealed class UserSummaryDto
        {
            [JsonPropertyName("user_id")]
            public Guid UserId { get; set; }

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }
        }
    }
    public class UsersToAddDto
    {
        [JsonProperty("id")] 
        public Guid Id { get; set; }
        [JsonProperty("username")] 
        public string Username { get; set; }
        [JsonProperty("avatar")] 
        public string? Avatar { get; set; }
        [JsonProperty("bio")] 
        public string? Bio { get; set; }
        [JsonProperty("first_name")] 
        public string? FirstName { get; set; }
        [JsonProperty("last_name")] 
        public string? LastName { get; set; }
        [JsonProperty("banner_image")] 
        public string? BannerImage { get; set; }
        [JsonProperty("country_of_origin")] 
        public string? CountryOfOrigin { get; set; }
        [JsonProperty("preferred_madhab")] 
        public string? PreferredMadhab { get; set; }
        [JsonProperty("hobbies")] 
        public List<string> Hobbies { get; set; } = new();
        [JsonProperty("favorite_quran_reciters")] 
        public List<string> FavoriteQuranReciters { get; set; } = new();
        [JsonProperty("favorite_islamic_scholars")] 
        public List<string> FavoriteIslamicScholars { get; set; } = new();
        [JsonProperty("islamic_study_topics")] 
        public List<string> IslamicStudyTopics { get; set; } = new();
        [JsonProperty("follower_count")] 
        public long FollowerCount { get; set; }
        [JsonProperty("following_count")] 
        public long FollowingCount { get; set; }
        [JsonProperty("total_items")] 
        public int TotalItems { get; set; }
    }
}
