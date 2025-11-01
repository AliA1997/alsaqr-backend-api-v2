using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Data.Entities.Meetup
{
    [Table("topics")]
    public class Topics: BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
