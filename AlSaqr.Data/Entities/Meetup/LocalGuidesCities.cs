using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("local_guides_cities")]
    public class LocalGuidesCities: BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("local_guides_id")]
        public int LocalGuidesId { get; set; }

        [Column("city_id")]
        public int CityId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
