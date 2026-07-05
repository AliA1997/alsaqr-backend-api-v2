using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.Meetup
{
    [Table("group_members")]
    public class GroupMember : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("group_id")]
        public Guid GroupId { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("role")]
        public string Role { get; set; } = "member";
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    }
}
