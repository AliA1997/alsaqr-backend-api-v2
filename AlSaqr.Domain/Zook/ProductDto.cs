using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Domain.Zook
{
    public class ProductDto
    {
        [JsonProperty("id")]
        public long? Id { get; set; }
        [JsonProperty("user_id")]
        public string? UserId { get; set; }
        [JsonProperty("title")]
        public string? Title { get; set; }
        [JsonProperty("description")]
        public string? Description { get; set; }
        [JsonProperty("price")]
        public double? Price { get; set; }
        [JsonProperty("images")]
        public string[]? Images { get; set; }
        [JsonProperty("slug")]
        public string? Slug { get; set; }
        [JsonProperty("attributes")]
        public IDictionary<string, object>? Attributes { get; set; }
        [JsonProperty("country")]
        public string Country { get; set; }
        [JsonProperty("longitude")]

        public decimal? Longitude { get; set; }
        [JsonProperty("latitude")]

        public decimal? Latitude { get; set; }
        [JsonProperty("distance_km")]
        public decimal? DistanceKm { get; set; }
        [JsonProperty("tags")]
        public string[]? Tags { get; set; }
        [JsonProperty("product_category_id")]
        public long ProductCategoryId { get; set; }
        [JsonProperty("category")]
        public string Category { get; set; }
    }
}
