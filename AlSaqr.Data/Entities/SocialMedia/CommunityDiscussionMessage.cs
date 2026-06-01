using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("community_discussion_messages")]
    public class CommunityDiscussionMessage : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("community_discussion_id")]
        public Guid CommunityDiscussionId { get; set; }
        [Column("creator_id")]
        public Guid CreatorId { get; set; }
        [Column("content")]
        public string Content { get; set; }
        [Column("media")]
        public string Media { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

    }
}
