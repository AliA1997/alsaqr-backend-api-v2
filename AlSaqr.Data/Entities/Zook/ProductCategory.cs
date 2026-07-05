using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Zook
{

    [Table("product_categories")]
    public class ProductCategory : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("product_category_type")]
        public int ProductCategoryType { get; set; } = 0;
    }
}
