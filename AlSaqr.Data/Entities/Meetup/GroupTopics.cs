using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.Meetup
{
    [Table("group_topics")]
    public class GroupTopics: BaseModel
    {
        [Column("id")]
        public Guid Id { get; set; }
        [Column("group_id")]
        public Guid GroupId { get; set; }
        [Column("topic_id")]
        public Guid TopicId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
