using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("group_attendees")]
    public class GroupAttendees: BaseModel
    {
        [Column("id")]
        public Guid Id { get; set; }
        [Column("group_id")]
        public Guid GroupId { get; set; }
        [Column("attendee_id")]
        public Guid AttendeeId { get; set; }
        [Column("is_group_organizer")]
        public bool IsGroupOrganizer { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
