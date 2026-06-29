using Newtonsoft.Json;

namespace AlSaqr.Domain.Meetup
{
    public class GroupDto
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
        [JsonProperty("slug")]
        public string? Slug { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("description")]
        public string? Description { get; set; }
        [JsonProperty("images")]
        public string[]? Images { get; set; }
        [JsonProperty("hq_city_id")]
        public Guid? CityId { get; set; }
        [JsonProperty("hq_city")]
        public string? City { get; set; }
        [JsonProperty("hq_country")]
        public string? Country { get; set; }
        [JsonProperty("topics")]
        public IDictionary<string, object>[]? Topics { get; set; }
       
        [JsonProperty("attendees")]
        public IDictionary<string, object>[]? Attendees { get; set; }

        [JsonProperty("hq_longitude")]

        public decimal? Longitude { get; set; }
        [JsonProperty("hq_latitude")]

        public decimal? Latitude { get; set; }
        [JsonProperty("founder_id")]
        public Guid? FounderId { get; set; }
        [JsonProperty("distance_km")]
        public decimal? DistanceKm { get; set; }

    }
}
