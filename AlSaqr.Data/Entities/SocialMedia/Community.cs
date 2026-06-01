using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("communities")]
    public class Community : BaseModel
    {
        public Community() { }

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("founder_id")]
        public Guid FounderId { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("description")]
        public string? Description { get; set; }
        [Column("avatar")]
        public string? Avatar { get; set; }
        [Column("banner_image")]
        public string? BannerImage { get; set; }
        [Column("tags")]
        public string[]? Tags { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

    }
}
