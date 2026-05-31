using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using static AlSaqr.Domain.SocialMedia.User;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class Posts
    {
        public class CreatePostDto
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public string? Image { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
            public string _Rev { get; set; }
            public string _Type { get; set; } = "post";
            public bool BlockTweet { get; set; }
            public string[] Tags { get; set; }
            public string[]? Likes { get; set; }
            public string? UserId { get; set; }
        }

        public class CreateCommentDto
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public string? CommentToCommentOnId { get; set; }
            public string PostId { get; set; }
            public string UserId { get; set; }
            public string? Image { get; set; }
            public string? CreatedAt { get; set; }
            public string? UpdatedAt { get; set; }
        }
    }

    public sealed class PostAuthorDto
    {
        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; } = string.Empty;

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }
    }


    public sealed class PostEngagementDto
    {
        [JsonPropertyName("likers")]
        public List<UserSummaryDto> Likers { get; set; } = [];

        [JsonPropertyName("reposters")]
        public List<UserSummaryDto> Reposters { get; set; } = [];
        [JsonPropertyName("bookmarkers")]
        public List<UserSummaryDto> Bookmarkers { get; set; } = [];

        [JsonPropertyName("likeCount")]
        public long LikeCount { get; set; }

        [JsonPropertyName("repostCount")]
        public long RepostCount { get; set; }

        [JsonPropertyName("bookmarkedCount")]
        public long BookmarkedCount { get; set; }
    }

    public class PostDto
    {

        public PostDto(dynamic postDetails)
        {
            PostId  = postDetails.PostId;
            Content = postDetails.Content;
            PostType = postDetails.PostType;
            RelatedPostId = postDetails.RelatedPostId;
            PostAvatar = postDetails.PostAvatar;
            PostBannerImage = postDetails.PostBannerImage;
            PostTags = postDetails.PostTags;
            CreatedAt = postDetails.PostCreatedAt;
            UpdatedAt = postDetails.PostUpdatedAt;
            Username = postDetails.Username;
            ProfileImg = postDetails.ProfileImg;
            AuthorBio = postDetails.AuthorBio;

            //Aggregate Columns
            Comments = postDetails.Comments;
            Commenters = postDetails.Commenters;
            Reposters = postDetails.Reposters;
            RepostCount = postDetails.RepostCount;
            Likers = postDetails.Likers;
            LikeCount = postDetails.LikeCount;
            Bookmarkers = postDetails.Bookmarkers;
            BookmarkCount = postDetails.BookmarkCount;
        }
        [JsonPropertyName("post_id")]
        public Guid PostId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("post_type")]
        public string PostType { get; set; } = string.Empty;

        [JsonPropertyName("related_post_id")]
        public Guid? RelatedPostId { get; set; }

        [JsonPropertyName("post_avatar")]
        public string? PostAvatar { get; set; }

        [JsonPropertyName("post_banner_image")]
        public string? PostBannerImage { get; set; }

        [JsonPropertyName("post_tags")]
        public string[]? PostTags { get; set; }

        [JsonPropertyName("post_created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("post_updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
        [JsonPropertyName("profile_img")]
        public string? ProfileImg { get; set; }
        [JsonPropertyName("author_bio")]
        public string? AuthorBio { get; set; }

        // Aggregated collections (JSONB → string; deserialize downstream)
        [JsonPropertyName("comments")]
        public PostCommentDto[]? Comments { get; set; } = new PostCommentDto[] { };

        [JsonPropertyName("commenters")]
        public PostUserInfoDto[]? Commenters { get; set; } = new PostUserInfoDto[] { };

        [JsonPropertyName("reposters")]
        public PostUserInfoDto[]? Reposters { get; set; } = new PostUserInfoDto[] { };

        [JsonPropertyName("likers")]
        public PostUserInfoDto[]? Likers { get; set; } = new PostUserInfoDto[] { };

        [JsonPropertyName("bookmarkers")]
        public PostUserInfoDto[]? Bookmarkers { get; set; } = new PostUserInfoDto[] { };

        // Counts
        [JsonPropertyName("comment_count")]
        public long CommentCount { get; set; }

        [JsonPropertyName("repost_count")]
        public long RepostCount { get; set; }

        [JsonPropertyName("like_count")]
        public long LikeCount { get; set; }

        [JsonPropertyName("bookmark_count")]
        public long BookmarkCount { get; set; }
    }

    public class PostUserInfoDto
    {
        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }
        [JsonPropertyName("username")]
        public string? Username { get; set; }
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }

    public class PostCommentDto
    {
        [JsonPropertyName("comment_id")]
        public Guid CommentId { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }
        [JsonPropertyName("username")]
        public string? Username { get; set; }
        [JsonPropertyName("avatar")]
        public object? Avatar { get; set; }
    }

    public class PostsToAddDto
    {
        [JsonProperty("post_id")] 
        public Guid PostId { get; set; }
        [JsonProperty("user_id")] 
        public Guid UserId { get; set; }
        [JsonProperty("content")] 
        public string Content { get; set; }
        [JsonProperty("post_type")] 
        public string PostType { get; set; }
        [JsonProperty("related_post_id")] 
        public Guid? RelatedPostId { get; set; }
        [JsonProperty("post_avatar")] 
        public string? PostAvatar { get; set; }
        [JsonProperty("post_banner_image")] 
        public string? PostBannerImage { get; set; }
        [JsonProperty("post_tags")] 
        public List<string> PostTags { get; set; } = new();
        [JsonProperty("post_created_at")] 
        public DateTime PostCreatedAt { get; set; }
        [JsonProperty("post_updated_at")] 
        public DateTime? PostUpdatedAt { get; set; }
        [JsonProperty("username")] 
        public string Username { get; set; }
        [JsonProperty("profile_img")] 
        public string? ProfileImg { get; set; }
        [JsonProperty("author_bio")] 
        public string? AuthorBio { get; set; }
        [JsonProperty("comments")] 
        public JArray Comments { get; set; } = new();
        [JsonProperty("commenters")] 
        public JArray Commenters { get; set; } = new();
        [JsonProperty("comment_count")] 
        public long CommentCount { get; set; }
        [JsonProperty("reposters")] 
        public JArray Reposters { get; set; } = new();
        [JsonProperty("repost_count")] 
        public long RepostCount { get; set; }
        [JsonProperty("likers")] 
        public JArray Likers { get; set; } = new();
        [JsonProperty("like_count")] 
        public long LikeCount { get; set; }
        [JsonProperty("bookmarkers")] 
        public JArray Bookmarkers { get; set; } = new();
        [JsonProperty("bookmark_count")] 
        public long BookmarkCount { get; set; }
        [JsonProperty("total_items")] 
        public int TotalItems { get; set; }
    }
}
