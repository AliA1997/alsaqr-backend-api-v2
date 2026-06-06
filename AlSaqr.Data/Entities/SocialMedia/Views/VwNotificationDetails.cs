using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_notification_details")]
    public class VwNotificationDetails : BaseModel
    {
        [PrimaryKey("notification_id", false)]
        public Guid NotificationId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("notification_type")]
        public string NotificationType { get; set; } = string.Empty;

        [Column("notification_message")]
        public string? NotificationMessage { get; set; }
        [Column("link")]
        public string? Link { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("notification_created_at")]
        public DateTime NotificationCreatedAt { get; set; }

        [Column("notification_updated_at")]
        public DateTime? NotificationUpdatedAt { get; set; }

        // Actor
        [Column("logged_in_user_id")]
        public Guid? LoggedInUserId { get; set; }

        [Column("logged_in_user_username")]
        public string? LoggedInUserUsername { get; set; }

        [Column("logged_in_user_avatar")]
        public string? LoggedInUserAvatar { get; set; }

        // Referenced entities
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

        [Column("list_item_id")]
        public Guid? ListItemId { get; set; }
    }
}
