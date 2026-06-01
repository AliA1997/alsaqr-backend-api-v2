using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("community_members")]
    public class CommunityMember : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("community_id")]
        public Guid CommunityId { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("role")]
        public string Role { get; set; } = "member";
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    }
}
