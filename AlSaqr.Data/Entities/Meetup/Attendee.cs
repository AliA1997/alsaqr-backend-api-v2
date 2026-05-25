using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("attendees")]
    public class Attendee: BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("user_id")]
        public Guid? UserId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
