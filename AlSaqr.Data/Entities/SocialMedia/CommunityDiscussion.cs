using AlSaqr.Domain.SocialMedia;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("community_discussions")]
    public class CommunityDiscussion : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("community_id")]
        public Guid CommunityId { get; set; }
        [Column("creator_id")]
        public Guid CreatorId { get; set; }
        [Column("title")]
        public string Title { get; set; }
        [Column("content")]
        public string Content { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public Community? Community { get; set; }
        public AlSaqrUser? Creator { get; set; }
        public ICollection<CommunityDiscussionMember> Members { get; set; } = new List<CommunityDiscussionMember>();
        //public ICollection<CommunityDiscussionMessage> Messages { get; set; } = new List<CommunityDiscussionMessage>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<ListItem> ListItems { get; set; } = new List<ListItem>();
    }
}
