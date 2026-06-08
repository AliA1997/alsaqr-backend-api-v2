using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            [JsonPropertyName("userToFollowId")]
            public Guid UserToFollowId { get; set; }
        }

        public class UnFollowUserFormDto
        {
            [JsonPropertyName("userToUnFollowId")]
            public Guid UserToUnFollowId { get; set; }
        }

        public class UserRegisterFormDto
        {
            public Guid[]? FollowingUsers { get; set; }
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

    public class UserToAdd
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string? Avatar { get; set; }
        public string? Bio { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? BannerImage { get; set; }
        public string? CountryOfOrigin { get; set; }
        public string? PreferredMadhab { get; set; }
        public List<string> Hobbies { get; set; } = new();
        public List<string> FavoriteQuranReciters { get; set; } = new();
        public List<string> FavoriteIslamicScholars { get; set; } = new();
        public List<string> IslamicStudyTopics { get; set; } = new();
        public long FollowerCount { get; set; }
        public long FollowingCount { get; set; }
        public int TotalItems { get; set; }

        public UserToAdd() { }

        public UserToAdd(UsersToAddDto dto)
        {
            Id = dto.Id;
            Username = dto.Username;
            Avatar = dto.Avatar;
            Bio = dto.Bio;
            FirstName = dto.FirstName;
            LastName = dto.LastName;
            BannerImage = dto.BannerImage;
            CountryOfOrigin = dto.CountryOfOrigin;
            PreferredMadhab = dto.PreferredMadhab;
            Hobbies = dto.Hobbies ?? new();
            FavoriteQuranReciters = dto.FavoriteQuranReciters ?? new();
            FavoriteIslamicScholars = dto.FavoriteIslamicScholars ?? new();
            IslamicStudyTopics = dto.IslamicStudyTopics ?? new();
            FollowerCount = dto.FollowerCount;
            FollowingCount = dto.FollowingCount;
            TotalItems = dto.TotalItems;
        }
    }

    public sealed class UserSummaryFollowDto
    {
        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }
        [JsonPropertyName("username")]
        public string Username { get; set; }
        [JsonPropertyName("avatar")]
        public string Avatar { get; set; }
        [JsonPropertyName("bio")]
        public string Bio { get; set; }
        
    }

    public class ProfileInfoDto
    {
        [JsonProperty("userId")]
        public Guid UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("avatar")]
        public string? Avatar { get; set; }
        [JsonProperty("bgThumbnail")]
        public string? BgThumbnail { get; set; }
        [JsonProperty("bannerImage")]
        public string? BannerImage { get; set; }

        [JsonProperty("bio")]
        public string? Bio { get; set; }

        [JsonProperty("firstName")]
        public string? FirstName { get; set; }

        [JsonProperty("lastName")]
        public string? LastName { get; set; }
        [JsonProperty("dateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonProperty("bookmarks")]
        public Guid[] Bookmarks { get; set; } = Array.Empty<Guid>();

        [JsonProperty("bookmarkCount")]
        public long BookmarkCount { get; set; }

        [JsonProperty("following")]
        public IDictionary<string, object>[]? Following { get; set; }

        [JsonProperty("followingCount")]
        public long FollowingCount { get; set; }

        [JsonProperty("followers")]
        public IDictionary<string, object>[]? Followers { get; set; }

        [JsonProperty("followerCount")]
        public long FollowerCount { get; set; }

        public ProfileInfoDto() { }

        public ProfileInfoDto(dynamic view)
        {
            UserId = view.UserId;
            Username = view.Username;
            Avatar = view.Avatar;
            BannerImage = view.BannerImage;
            Bio = view.Bio;
            FirstName = view.FirstName;
            LastName = view.LastName;
            DateOfBirth = view.DateOfBirth;
            CreatedAt = view.UserCreatedAt;
            UpdatedAt = view.UserUpdatedAt;

            Bookmarks = view.BookmarkIds ?? Array.Empty<Guid>();
            BookmarkCount = view.BookmarkCount;

            Following = view.Following;
            FollowingCount = view.FollowingCount;

            Followers = view.Followers;
            FollowerCount = view.FollowerCount;
        }
    }

    /// <summary>
    /// A single profile post row. Mirrors a Neo4j UNION row sourced from
    /// vw_user_profile_posts. The JSONB engagement columns are surfaced as
    /// parsed arrays.
    /// </summary>
    public class ProfilePostDto
    {
        [JsonProperty("postId")]
        public Guid PostId { get; set; }

        [JsonProperty("postRelationType")]
        public string PostRelationType { get; set; } = string.Empty;

        [JsonProperty("postOwnerId")]
        public Guid PostOwnerId { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        [JsonProperty("postType")]
        public string PostType { get; set; } = string.Empty;

        [JsonProperty("relatedPostId")]
        public Guid? RelatedPostId { get; set; }

        [JsonProperty("postAvatar")]
        public string? PostAvatar { get; set; }

        [JsonProperty("bannerImage")]
        public string? BannerImage { get; set; }

        [JsonProperty("tags")]
        public string[]? Tags { get; set; }

        [JsonProperty("postCreatedAt")]
        public DateTime PostCreatedAt { get; set; }

        [JsonProperty("postUpdatedAt")]
        public DateTime? PostUpdatedAt { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("profileImg")]
        public string? ProfileImg { get; set; }

        [JsonProperty("likers")]
        public IDictionary<string, object>[]? Likers { get; set; }

        [JsonProperty("reposters")]
        public IDictionary<string, object>[]? Reposters { get; set; }

        [JsonProperty("bookmarkers")]
        public IDictionary<string, object>[]? Bookmarkers { get; set; }

        [JsonProperty("comments")]
        public IDictionary<string, object>[]? Comments { get; set; }

        [JsonProperty("commenters")]
        public IDictionary<string, object>[]? Commenters { get; set; }

        [JsonProperty("likeCount")]
        public long? LikeCount { get; set; }

        [JsonProperty("repostCount")]
        public long? RepostCount { get; set; }

        [JsonProperty("bookmarkCount")]
        public long? BookmarkCount { get; set; }

        [JsonProperty("commentCount")]
        public long? CommentCount { get; set; }

        public ProfilePostDto() { }

        public ProfilePostDto(dynamic view)
        {
            PostId = view.PostId;
            PostRelationType = view.PostRelationType;
            PostOwnerId = view.PostOwnerId;
            Content = view.Content;
            PostType = view.PostType;
            RelatedPostId = view.RelatedPostId;
            PostAvatar = view.PostAvatar;
            BannerImage = view.BannerImage;
            Tags = view.Tags;
            PostCreatedAt = view.PostCreatedAt;
            PostUpdatedAt = view.PostUpdatedAt;
            Username = view.Username;
            ProfileImg = view.ProfileImg;

            Likers = view.Likers;
            Reposters = view.Reposters;
            Bookmarkers = view.Bookmarkers;
            Comments = view.Comments;
            Commenters = view.Commenters;

            LikeCount = view.LikeCount;
            RepostCount = view.RepostCount;
            BookmarkCount = view.BookmarkCount;
            CommentCount = view.CommentCount;
        }
    }

    /// <summary>
    /// Bucketed profile-posts payload, replacing the five Neo4j UNION groups.
    /// </summary>
    public class UserProfilePostsDto
    {
        [JsonProperty("userPosts")]
        public List<ProfilePostDto> UserPosts { get; set; } = new();

        [JsonProperty("bookmarkedPosts")]
        public List<ProfilePostDto> BookmarkedPosts { get; set; } = new();

        [JsonProperty("likedPosts")]
        public List<ProfilePostDto> LikedPosts { get; set; } = new();

        [JsonProperty("repostedPosts")]
        public List<ProfilePostDto> RepostedPosts { get; set; } = new();

        [JsonProperty("repliedPosts")]
        public List<ProfilePostDto> RepliedPosts { get; set; } = new();
    }
}
