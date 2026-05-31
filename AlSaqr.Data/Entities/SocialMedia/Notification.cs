using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("notifications")]
    public class Notification : BaseModel
    {
        public Notification() { }

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("message")]
        public string Message { get; set; }
        [Column("notification_type")]
        public string NotificationType { get; set; }
        [Column("read")]
        public bool? Read { get; set; } = false;
        [Column("item_type")]
        public string ItemType { get; set; }
        [Column("related_user_id")]
        public Guid? RelatedUserId { get; set; }
        [Column("post_id")]
        public Guid? PostId { get; set; }
        [Column("community_id")]
        public Guid? CommunityId { get; set; }
        [Column("community_discussion_id")]
        public Guid? CommunityDiscussionId { get; set; }
        [Column("community_discussion_message_id")]
        public Guid? CommunityDiscussionMessageId { get; set; }
        [Column("list_id")]
        public Guid? ListId { get; set; }
        [Column("link")]
        public string? Link { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public AlSaqrUser? User { get; set; }
        public AlSaqrUser? RelatedUser { get; set; }
        //public Post? Post { get; set; }
        //public Community? Community { get; set; }
        //public CommunityDiscussion? CommunityDiscussion { get; set; }
        //public CommunityDiscussionMessage? CommunityDiscussionMessage { get; set; }
        //public List? List { get; set; }
    }
}
