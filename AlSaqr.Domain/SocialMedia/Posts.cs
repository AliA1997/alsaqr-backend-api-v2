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
            public string Text { get; set; }
            public string? Image { get; set; }
            public string[] Tags { get; set; }
        }

        public class CreateCommentDto
        {
            public string Text { get; set; }
            public Guid PostId { get; set; }
            public Guid UserId { get; set; }
            public string? Image { get; set; }
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
        [JsonPropertyName("postId")]
        public Guid PostId { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("postType")]
        public string PostType { get; set; } = string.Empty;

        [JsonPropertyName("relatedPostId")]
        public Guid? RelatedPostId { get; set; }

        [JsonPropertyName("postAvatar")]
        public string? PostAvatar { get; set; }

        [JsonPropertyName("postBannerImage")]
        public string? PostBannerImage { get; set; }

        [JsonPropertyName("postTags")]
        public string[]? PostTags { get; set; }

        [JsonPropertyName("postCreatedAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("postUpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
        [JsonPropertyName("profileImg")]
        public string? ProfileImg { get; set; }
        [JsonPropertyName("authorBio")]
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
        [JsonPropertyName("commentCount")]
        public long CommentCount { get; set; }

        [JsonPropertyName("repostCount")]
        public long RepostCount { get; set; }

        [JsonPropertyName("likeCount")]
        public long LikeCount { get; set; }

        [JsonPropertyName("bookmarkCount")]
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

    public class PostsToAdd
    {
        public PostsToAdd(PostsToAddDto details)
        {
            this.PostId = details.PostId;
            this.UserId = details.UserId;
            this.Content = details.Content;
            this.PostType = details.PostType;
            this.RelatedPostId = details.RelatedPostId;
            this.PostAvatar = details.PostAvatar;
            this.PostBannerImage = details.PostBannerImage;
            this.PostTags = details.PostTags;
            this.PostCreatedAt = details.PostCreatedAt;
            this.PostUpdatedAt = details.PostUpdatedAt;
            this.Username = details.Username;
            this.ProfileImg = details.ProfileImg;
            this.AuthorBio = details.AuthorBio;
            this.Comments = details.Comments;
            this.Commenters = details.Commenters;
            this.CommentCount = details.CommentCount;

            this.Reposters = details.Reposters;
            this.RepostCount = details.RepostCount;

            this.Likers = details.Likers;
            this.LikeCount = details.LikeCount;

            this.Bookmarkers = details.Bookmarkers;
            this.BookmarkCount = details.BookmarkCount;

            this.TotalItems = details.TotalItems;
        }

        [JsonProperty("postId")] 
        public Guid PostId { get; set; }
        [JsonProperty("userId")] 
        public Guid UserId { get; set; }
        [JsonProperty("content")] 
        public string Content { get; set; }
        [JsonProperty("postType")] 
        public string PostType { get; set; }
        [JsonProperty("relatedPostId")] 
        public Guid? RelatedPostId { get; set; }
        [JsonProperty("postAvatar")] 
        public string? PostAvatar { get; set; }
        [JsonProperty("postBannerImage")] 
        public string? PostBannerImage { get; set; }
        [JsonProperty("postTags")] 
        public List<string> PostTags { get; set; } = new();
        [JsonProperty("postCreatedAt")] 
        public DateTime PostCreatedAt { get; set; }
        [JsonProperty("postUpdatedAt")] 
        public DateTime? PostUpdatedAt { get; set; }
        [JsonProperty("username")] 
        public string Username { get; set; }
        [JsonProperty("profileImg")] 
        public string? ProfileImg { get; set; }
        [JsonProperty("authorBio")] 
        public string? AuthorBio { get; set; }
        [JsonProperty("comments")] 
        public JArray Comments { get; set; } = new();
        [JsonProperty("commenters")] 
        public JArray Commenters { get; set; } = new();
        [JsonProperty("commentCount")] 
        public long CommentCount { get; set; }
        [JsonProperty("reposters")] 
        public JArray Reposters { get; set; } = new();
        [JsonProperty("repostCount")] 
        public long RepostCount { get; set; }
        [JsonProperty("likers")] 
        public JArray Likers { get; set; } = new();
        [JsonProperty("likeCount")] 
        public long LikeCount { get; set; }
        [JsonProperty("bookmarkers")] 
        public JArray Bookmarkers { get; set; } = new();
        [JsonProperty("bookmarkCount")] 
        public long BookmarkCount { get; set; }
        [JsonProperty("totalItems")] 
        public int TotalItems { get; set; }
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
