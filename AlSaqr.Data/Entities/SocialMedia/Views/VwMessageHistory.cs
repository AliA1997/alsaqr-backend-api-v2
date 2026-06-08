using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia.Views
{
    // ============================================================
    // vw_message_history
    // One row per (viewer, partner) conversation thread.
    // Filter on ViewerId to scope to the logged-in user's inbox.
    // ============================================================
    [Table("vw_message_history")]
    public sealed class VwMessageHistory : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        // Filter handle for the current user's threads — not surfaced in the DTO.
        [Column("viewer_id")]
        public Guid ViewerId { get; set; }

        [Column("receiver_id")]
        public Guid ReceiverId { get; set; }

        [Column("receiver_profile_image")]
        public string? ReceiverProfileImage { get; set; }

        [Column("receiver_username")]
        public string? ReceiverUsername { get; set; } = string.Empty;

        [Column("message_count")]
        public long MessageCount { get; set; }

        [Column("last_message_date")]
        public DateTime LastMessageDate { get; set; }
    }
}
