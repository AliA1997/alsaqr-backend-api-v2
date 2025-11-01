using Newtonsoft.Json;

namespace AlSaqr.Domain.Meetup
{
    public class GroupDto
    {
        [JsonProperty("id")]
        public long? Id { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("description")]
        public string? Description { get; set; }
        [JsonProperty("images")]
        public IDictionary<string, object>[]? Images { get; set; }
        [JsonProperty("hq_city_id")]
        public long? CityId { get; set; }
        [JsonProperty("hq_city")]
        public string? City { get; set; }
        [JsonProperty("hq_country")]
        public string? Country { get; set; }
        [JsonProperty("topics")]
        public IDictionary<string, object>[]? Topics { get; set; }
       
        [JsonProperty("attendees")]
        public IDictionary<string, object>[]? Attendees { get; set; }

        [JsonProperty("longitude")]

        public decimal? Longitude { get; set; }
        [JsonProperty("latitude")]

        public decimal? Latitude { get; set; }
        [JsonProperty("distance_km")]
        public decimal? DistanceKm { get; set; }
    }
}
