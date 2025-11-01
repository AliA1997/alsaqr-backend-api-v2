using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("events")]
    public class Event : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        [Column("slug")]
        public string? Slug { get; set; }

        [Column("name")]
        public string? Name { get; set; }
        [Column("description")]
        public string? Description { get; set; }
        [Column("images")]
        public IDictionary<string, object>[] Images { get; set; }
        [Column("group_id")]
        public int? GroupId { get; set; }
        [Column("is_online")]
        public bool IsOnline { get; set; }
        [Column("last_occurred_at")]
        public DateTime? LastOccurredAt { get; set; }
        [Column("times_occurred")]
        public int? TimesOccurred { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

    }
}
