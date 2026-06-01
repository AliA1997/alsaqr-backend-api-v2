using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("posts")]
    public class Post : BaseModel
    {
        public Post() { }

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("content")]
        public string Content { get; set; }
        [Column("post_type")]
        public string PostType { get; set; }
        [Column("related_post_id")]
        public Guid? RelatedPostId { get; set; }
        [Column("avatar")]
        public string? Avatar { get; set; }
        [Column("banner_image")]
        public string? BannerImage { get; set; }
        [Column("tags")]
        public string[]? Tags { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

    }
}
