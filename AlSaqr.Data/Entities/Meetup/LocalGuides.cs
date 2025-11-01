using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("local_guides")]
    public class LocalGuides: BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("neo4j_user_id")]
        public string Neo4jUserId { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
