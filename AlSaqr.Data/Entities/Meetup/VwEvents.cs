using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("vw_events")]
    public class VwEvent : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }
        [Column("slug")]
        public string? Slug { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("images")]
        public string[] Images { get; set; }

        [Column("group_id")]
        public Guid GroupId { get; set; }
        
        [Column("group_name")]
        public string? GroupName { get; set; }
        [Column("group_founder_id")]
        public Guid? GroupFounderId { get; set; }

        [Column("cities_hosted")]
        public IDictionary<string, object>[] CitiesHosted { get; set; }

    }
}
