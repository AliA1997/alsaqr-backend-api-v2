using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// Read model for an event a user attended, sourced from vw_event_attendees.
    /// A user "attends" an event by being an attendee of the group that hosts it,
    /// so rows are keyed by the attendee's username and annotated with when that
    /// attendance began (joined_at).
    /// </summary>
    [Table("vw_event_attendees")]
    public class VwEventAttendees : BaseModel
    {
        // Event identity.
        [PrimaryKey("event_id", false)]
        public Guid EventId { get; set; }
        [PrimaryKey("user_id", false)]
        public Guid UserId { get; set; }
    

        [Column("event_slug")]
        public string? EventSlug { get; set; }

        [Column("event_name")]
        public string? EventName { get; set; }

        [Column("event_description")]
        public string? EventDescription { get; set; }

        [Column("event_images")]
        public string[]? EventImages { get; set; }

        // Host group.
        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("group_name")]
        public string? GroupName { get; set; }

        // json aggregate of { id, name, latitude, longitude } city objects; empty array when none.
        [Column("cities_hosted")]
        public IDictionary<string, object>[]? CitiesHosted { get; set; }

        // Who attended and when their attendance began.
        [Column("username")]
        public string? Username { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }
    }
}
