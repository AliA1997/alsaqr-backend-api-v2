using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("cities")]
    public class City : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("state_or_province")]
        public string? StateOrProvince { get; set; }

        [Column("country")]
        public string? Country { get; set; }
        [Column("latitude")]
        public decimal? Latitude { get; set; }
        [Column("longitude")]
        public decimal? Longitude { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
