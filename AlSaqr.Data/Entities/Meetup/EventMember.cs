using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.Meetup
{
    [Table("event_members")]
    public class EventMember : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("event_id")]
        public Guid EventId { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("role")]
        public string Role { get; set; } = "member";
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    }
}
