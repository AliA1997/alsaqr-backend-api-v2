using AlSaqr.Domain.Meetup;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("vw_local_guides")]
    public class VwLocalGuides : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }
        [Column("slug")]
        public string? Slug { get; set; }
        [Column("userid")]
        public Guid UserId { get; set; }
        [Column("name")]
        public string Name { get; set; }

        [Column("cities_hosted")]
        //public string? CitiesHosted { get; set; }
        public IDictionary<string, object>[]? CitiesHosted { get; set; }
        
        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; }

    }
}
