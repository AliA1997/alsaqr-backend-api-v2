using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("attendees")]
    public class Attendee: BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("neo4j_user_id")]
        public string? Neo4jUserId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
