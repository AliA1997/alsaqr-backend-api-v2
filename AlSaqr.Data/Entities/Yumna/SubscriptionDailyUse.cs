using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Yumna
{
    [Table("subscription_daily_use")]
    public class SubscriptionDailyUse : BaseModel
    {
        public SubscriptionDailyUse() { }

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("date")]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;
        [Column("number_of_requests")]
        public int NumberOfRequests { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
