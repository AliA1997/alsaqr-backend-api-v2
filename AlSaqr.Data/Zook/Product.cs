using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Data.Zook
{

    [Table("products")]
    public class Product : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("title")]
        public string? Title { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("price")]
        public double? Price { get; set; }

        [Column("images")]
        public string[] Images { get; set; } 

        [Column("slug")]
        public string? Slug { get; set; }

        [Column("attributes")]
        public IDictionary<string, object> Attributes { get; set; }

        [Column("tags")]
        public string[]? Tags { get; set; }


        [Column("product_category_id")]
        public long ProductCategoryId { get; set; }

        [Column("latitude")]
        public double? Latitude { get; set; }
        [Column("longitude")]
        public double? Longitude { get; set; }

        [Column("country")]
        public string? Country { get; set; }

        [Column("neo4j_user_id")]
        public string? Neo4jUserId { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
