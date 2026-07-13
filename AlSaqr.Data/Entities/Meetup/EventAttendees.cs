using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// A user's attendance of a single event, keyed on the attendees table (not users
    /// directly) so it mirrors <see cref="GroupAttendees"/>. GroupId is the event's host
    /// group, denormalized onto the row.
    /// </summary>
    [Table("event_attendees")]
    public class EventAttendees : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("event_id")]
        public Guid EventId { get; set; }

        [Column("attendee_id")]
        public Guid AttendeeId { get; set; }

        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("is_event_organizer")]
        public bool IsEventOrganizer { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
