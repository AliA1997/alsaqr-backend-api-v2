using Newtonsoft.Json;

namespace AlSaqr.API.Utils
{
    public static class User
    {
        public class GetUserResponse
        {
            [JsonProperty("user")]
            public AlSaqr.Domain.Common.User User { get; set; }
            
        }
        public class UpdateUserDto
        {
            public string Id { get; set; }
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
            public string UserToFollowId { get; set; }
        }

        public class UnFollowUserFormDto
        {
            public string UserToUnFollowId { get; set; }
        }

        public class UserRegisterFormDto
        {
            public string[] FollowingUsers { get; set; }
            public string? Bio { get; set; }
            public string? Avatar { get; set; }
            public string? BgThumbnail { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime DateOfBirth { get; set; }
            public string? MaritalStatus { get; set; }
            public string[]? Hobbies { get; set; }
            public string? Religion { get; set; }
            public string? CountryOfOrigin { get; set; }


        }
    }
}
