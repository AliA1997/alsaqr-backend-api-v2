using Microsoft.AspNetCore.Identity;

namespace AlSaqr.Domain.Common
{
   public class User : IdentityUser<string>
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }

        public string? Avatar { get; set; }
        public string? BgThumbnail { get; set; }
        public string? FrequentMasjid { get; set; }
        public string? Bio { get; set; }

        public DateTimeOffset? DateOfBirth { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public object? UpdatedAt { get; set; }

        public bool Verified { get; set; } = false;
        public bool IsCompleted { get; set; } = false;

        public string? PreferredMadhab { get; set; }
        public string? Religion { get; set; }
        public string? CountryOfOrigin { get; set; }
        public string? MaritalStatus { get; set; }

        public List<string> FavoriteQuranReciters { get; set; } = new();
        public List<string> IslamicStudyTopics { get; set; } = new();
        public List<string> FavoriteIslamicScholars { get; set; } = new();
        public List<string> Hobbies { get; set; } = new();

        public List<string> FollowingUsers { get; set; } = new();

    }
}
