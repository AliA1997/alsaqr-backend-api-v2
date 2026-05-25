using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("local_guides")]
    public class LocalGuides: BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }
        [Column("slug")]
        public string? Slug { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
