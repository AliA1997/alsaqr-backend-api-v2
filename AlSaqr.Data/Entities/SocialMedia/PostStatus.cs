using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("post_statuses")]
    public class PostStatus : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("post_id")]
        public Guid PostId { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("action")]
        public string Action { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        
    }
}
