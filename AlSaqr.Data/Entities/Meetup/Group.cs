using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("groups")]
    public class Groups : BaseModel
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("slug")]
        public string? Slug { get; set; }

        [Column("name")]
        public string? Name { get; set; }
        [Column("description")]
        public string? Description { get; set; }
        [Column("images")]
        public IDictionary<string, object>[] Images { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("hq_city_id")]
        public int? HqCityId { get; set; }
    }
}
