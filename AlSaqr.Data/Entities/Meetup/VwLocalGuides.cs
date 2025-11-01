using AlSaqr.Domain.Meetup;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("vw_local_guides")]
    public class VwLocalGuides : BaseModel
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("slug")]
        public string? Slug { get; set; }
        [Column("userid")]
        public string UserId { get; set; }
        [Column("name")]
        public string Name { get; set; }

        [Column("cities_hosted")]
        public List<LocalGuideCityDto> CitiesHosted { get; set; }
        
        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; }

    }
}
