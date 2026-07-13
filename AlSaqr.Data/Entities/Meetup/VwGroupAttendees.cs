using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// Read model for a group a user has joined, sourced from vw_group_attendees.
    /// One row per group_attendees record, resolved back to the user through the
    /// attendees table. Id is the group_attendees row; UserId is who joined.
    /// </summary>
    [Table("vw_group_attendees")]
    public class VwGroupAttendees : BaseModel
    {
        // Membership identity / who joined.
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [PrimaryKey("user_id", false)]
        public Guid UserId { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("is_group_organizer")]
        public bool IsGroupOrganizer { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }

        // Group identity.
        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("group_name")]
        public string? GroupName { get; set; }

        [Column("group_description")]
        public string? GroupDescription { get; set; }

        [Column("group_images")]
        public string[]? GroupImages { get; set; }

        [Column("group_slug")]
        public string? GroupSlug { get; set; }

        // Headquarters city.
        [Column("hq_city_id")]
        public Guid HqCityId { get; set; }

        [Column("hq_city")]
        public string? HqCity { get; set; }

        [Column("hq_country")]
        public string? HqCountry { get; set; }

        [Column("hq_latitude")]
        public decimal? HqLatitude { get; set; }

        [Column("hq_longitude")]
        public decimal? HqLongitude { get; set; }

        // json aggregate of { id, name } topic objects; empty array when none.
        [Column("topics")]
        public IDictionary<string, object>[]? Topics { get; set; }
    }
}
