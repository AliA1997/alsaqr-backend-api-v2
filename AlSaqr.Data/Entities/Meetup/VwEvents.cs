using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("vw_events")]
    public class VwEvent : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("images")]
        public IDictionary<string, object>[] Images { get; set; }

        [Column("group_id")]
        public int GroupId { get; set; }
        
        [Column("group_name")]
        public string? GroupName { get; set; }

        [Column("cities_hosted")]
        public IDictionary<string, object>[] CitiesHosted { get; set; }

    }
}
