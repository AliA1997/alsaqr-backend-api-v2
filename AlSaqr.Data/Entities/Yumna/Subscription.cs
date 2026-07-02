using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Yumna
{
    [Table("subscriptions")]
    public class Subscription : BaseModel
    {
        public Subscription() { }

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("name")]
        public string Name { get; set; }
        [Column("daily_request_limit")]
        public int DailyRequestLimit { get; set; } = 30;
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }
    }
}
