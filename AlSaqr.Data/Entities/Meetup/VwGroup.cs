using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("vw_groups")]
    public class VwGroup : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("images")]
        public IDictionary<string, object>[] Images { get; set; }

        [Column("hq_city_id")]
        public int HqCityId { get; set; }
        
        [Column("hq_city")]
        public string? HqCity { get; set; }

        [Column("hq_country")]
        public string? HqCountry { get; set; }

        [Column("description")]
        public string? Description { get; set; }


        [Column("topics")]
        public IDictionary<string, object>[] Topics { get; set; }

        [Column("attendees")]
        public IDictionary<string, object>[] Attendees { get; set; }
    }
}
