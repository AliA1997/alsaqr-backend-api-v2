using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("messages")]
    public class Message : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("sender_id")]
        public Guid SenderId { get; set; }

        [Column("recipient_id")]
        public Guid RecipientId { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("read")]
        public bool Read { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [Column("deleted_at")]
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
