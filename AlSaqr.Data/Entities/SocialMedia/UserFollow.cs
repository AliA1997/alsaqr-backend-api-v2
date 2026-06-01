using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("user_follows")]
    public class UserFollow : BaseModel
    {
        public UserFollow() { }


        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("follower_id")]
        public Guid FollowerId { get; set; }
        [Column("following_id")]
        public Guid FollowingId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
