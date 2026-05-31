using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using AlSaqr.Domain.SocialMedia;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_post_details")]
    public class VwPostDetails : BaseModel
    {
        public VwPostDetails() { }

        [PrimaryKey("post_id", false)]
        public Guid PostId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("post_type")]
        public string PostType { get; set; } = string.Empty;

        [Column("related_post_id")]
        public Guid? RelatedPostId { get; set; }

        [Column("post_avatar")]
        public string? PostAvatar { get; set; }

        [Column("post_banner_image")]
        public string? PostBannerImage { get; set; }

        [Column("post_tags")]
        public string[]? PostTags { get; set; } = new string[] { };

        [Column("post_created_at")]
        public DateTime PostCreatedAt { get; set; }

        [Column("post_updated_at")]
        public DateTime? PostUpdatedAt { get; set; }

        // Author
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("profile_img")]
        public string? ProfileImg { get; set; }

        [Column("author_bio")]
        public string? AuthorBio { get; set; }

         //Aggregated collections
        [Column("comments")]
        public PostCommentDto[]? Comments { get; set; } = new PostCommentDto[] { };

        [Column("commenters")]
        public PostUserInfoDto[]? Commenters { get; set; } = new PostUserInfoDto[] { };

        [Column("reposters")]
        public PostUserInfoDto[]? Reposters { get; set; } = new PostUserInfoDto[] { };

        [Column("likers")]
        public PostUserInfoDto[]? Likers { get; set; } = new PostUserInfoDto[] { };

        [Column("bookmarkers")]
        public PostUserInfoDto[]? Bookmarkers { get; set; } = new PostUserInfoDto[] { };

        // Counts
        [Column("comment_count")]
        public long CommentCount { get; set; }

        [Column("repost_count")]
        public long RepostCount { get; set; }

        [Column("like_count")]
        public long LikeCount { get; set; }

        [Column("bookmark_count")]
        public long BookmarkCount { get; set; }
    }

}
