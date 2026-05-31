using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("community_discussion_members")]
    public class CommunityDiscussionMember : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("community_discussion_id")]

        public Guid CommunityDiscussionId { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("role")]
        public string Role { get; set; } = "member";
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public CommunityDiscussion? CommunityDiscussion { get; set; }
        public AlSaqrUser? User { get; set; }
    }
}
