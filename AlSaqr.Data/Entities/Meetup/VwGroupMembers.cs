using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// Read model for the members belonging to a group, sourced from vw_group_members.
    /// One row per group_attendees record, resolved back to the user through the
    /// attendees table, and flagged when that user is also a local guide.
    /// Id is the group_attendees row; UserId is who joined.
    /// </summary>
    [Table("vw_group_members")]
    public class VwGroupMembers : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("avatar")]
        public string? Avatar { get; set; }

        [Column("hobbies")]
        public string[]? Hobbies { get; set; }

        [Column("is_group_organizer")]
        public bool IsGroupOrganizer { get; set; }

        [Column("is_local_guide")]
        public bool IsLocalGuide { get; set; }

        // When the member joined the group.
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
