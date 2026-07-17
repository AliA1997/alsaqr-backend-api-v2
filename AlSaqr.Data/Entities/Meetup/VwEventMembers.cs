using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// Read model for the members attending an event, sourced from vw_event_members.
    /// A row exists per event_attendees record, resolved back to the user through the
    /// attendees table, and flagged when that user is also a local guide.
    /// </summary>
    [Table("vw_event_members")]
    public class VwEventMembers : BaseModel
    {
        // A user appears at most once per event, so (event_id, user_id) identifies a row.
        [PrimaryKey("event_id", false)]
        public Guid EventId { get; set; }

        [PrimaryKey("user_id", false)]
        public Guid UserId { get; set; }

        [Column("group_id")]
        public Guid? GroupId { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("user_avatar")]
        public string? UserAvatar { get; set; }

        [Column("user_hobbies")]
        public string[]? UserHobbies { get; set; }

        [Column("is_event_organizer")]
        public bool IsEventOrganizer { get; set; }

        [Column("is_local_guide")]
        public bool IsLocalGuide { get; set; }

        // When the member joined the event.
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
