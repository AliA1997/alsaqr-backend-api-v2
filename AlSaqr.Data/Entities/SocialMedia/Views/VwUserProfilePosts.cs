using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia.Views
{

    [Table("vw_user_profile_posts")]
    public class VwUserProfilePosts : BaseModel
    {
        [PrimaryKey("post_id", false)]
        public Guid PostId { get; set; }

        [Column("viewer_user_id")]
        public Guid ViewerUserId { get; set; }

        [Column("post_relation_type")]
        public string PostRelationType { get; set; } = string.Empty;

        [Column("post_owner_id")]
        public Guid PostOwnerId { get; set; }

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("post_type")]
        public string PostType { get; set; } = string.Empty;

        [Column("related_post_id")]
        public Guid? RelatedPostId { get; set; }

        [Column("post_avatar")]
        public string? PostAvatar { get; set; }

        [Column("banner_image")]
        public string? BannerImage { get; set; }

        [Column("tags")]
        public string[]? Tags { get; set; }

        [Column("post_created_at")]
        public DateTimeOffset PostCreatedAt { get; set; }

        [Column("post_updated_at")]
        public DateTimeOffset? PostUpdatedAt { get; set; }

        // Author
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("profile_img")]
        public string? ProfileImg { get; set; }

        // Engagement (JSONB → string, deserialized in mapper)
        [Column("likers")]
        public string Likers { get; set; } = "[]";

        [Column("reposters")]
        public string Reposters { get; set; } = "[]";

        [Column("bookmarkers")]
        public string Bookmarkers { get; set; } = "[]";

        [Column("comments")]
        public string Comments { get; set; } = "[]";

        [Column("commenters")]
        public string Commenters { get; set; } = "[]";

        // Counts
        [Column("like_count")]
        public long LikeCount { get; set; }

        [Column("repost_count")]
        public long RepostCount { get; set; }

        [Column("bookmark_count")]
        public long BookmarkCount { get; set; }

        [Column("comment_count")]
        public long CommentCount { get; set; }
    }

}
