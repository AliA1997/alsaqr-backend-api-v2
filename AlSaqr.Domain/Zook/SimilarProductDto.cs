using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Domain.Zook
{
    public class SimilarProductDto:ProductDto
    {
        [JsonProperty("title_similarity")]
        public decimal? TitleSimilarity { get; set;}
        [JsonProperty("description_similarity")]

        public decimal? DescriptionSimilarity { get; set; }
        [JsonProperty("category_similarity")]

        public decimal? CategorySimilarity { get; set; }

        
    }
}
