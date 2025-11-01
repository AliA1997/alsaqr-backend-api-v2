using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("event_cities")]
    public class EventCities : BaseModel
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("city_id")]
        public int CityId { get; set; }
        [Column("event_id")]
        public int EventId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
