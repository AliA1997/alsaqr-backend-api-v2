using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("vw_local_guides")]
    public class VwLocalGuides : BaseModel
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("userid")]
        public string UserId { get; set; }
        [Column("name")]
        public string Name { get; set; }

        [Column("city")]
        public string City { get; set; }

        [Column("state_or_province")]
        public string StateOrProvince { get; set; }

        [Column("country")]
        public string Country { get; set; }

        [Column("latitude")]
        public decimal Latitude { get; set; }

        [Column("longitude")]
        public decimal Longitude { get; set; }

        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; }

    }
}
