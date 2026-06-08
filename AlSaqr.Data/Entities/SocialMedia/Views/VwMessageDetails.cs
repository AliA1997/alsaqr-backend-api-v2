using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    [Table("vw_message_details")]
    public class VwMessageDetails : BaseModel
    {
        [PrimaryKey("message_id", false)]
        public Guid MessageId { get; set; }

        [Column("sender_id")]
        public Guid SenderId { get; set; }

        [Column("recipient_id")]
        public Guid RecipientId { get; set; }

        [Column("message_content")]
        public string MessageContent { get; set; } = string.Empty;
        [Column("message_media")]
        public string MessageMedia { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("message_created_at")]
        public DateTimeOffset MessageCreatedAt { get; set; }

        [Column("message_updated_at")]
        public DateTimeOffset? MessageUpdatedAt { get; set; }

        // Sender
        [Column("sender_username")]
        public string SenderUsername { get; set; } = string.Empty;

        [Column("sender_avatar")]
        public string? SenderAvatar { get; set; }

        // Recipient
        [Column("recipient_username")]
        public string RecipientUsername { get; set; } = string.Empty;

        [Column("recipient_avatar")]
        public string? RecipientAvatar { get; set; }
    }
}
