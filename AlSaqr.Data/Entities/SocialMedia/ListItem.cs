using AlSaqr.Domain.SocialMedia;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("list_items")]
    public class ListItem : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("list_id")]
        public Guid ListId { get; set; }
        [Column("item_type")]
        public string ItemType { get; set; }
        [Column("user_id")]
        public Guid? UserId { get; set; }
        [Column("post_id")]
        public Guid? PostId { get; set; }
        [Column("community_id")]
        public Guid? CommunityId { get; set; }
        [Column("community_discussion_id")]
        public Guid? CommunityDiscussionId { get; set; }
        [Column("community_discussion_message_id")]
        public Guid? CommunityDiscussionMessageId { get; set; }
        [Column("saved_at")]
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        //public List? List { get; set; }
        public AlSaqrUser? User { get; set; }
        public Post? Post { get; set; }
        //public Community? Community { get; set; }
        //public CommunityDiscussion? CommunityDiscussion { get; set; }
        //public CommunityDiscussionMessage? CommunityDiscussionMessage { get; set; }
    }
}
