using Newtonsoft.Json;

namespace AlSaqr.Domain.Meetup
{
    public class LocalGuideCityDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("name")]
        public string City { get; set; }

        [JsonProperty("state_or_province")]
        public string StateOrProvince { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("latitude")]
        public decimal Latitude { get; set; }

        [JsonProperty("longitude")]
        public decimal Longitude { get; set; }
    }
    public class LocalGuideDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("slug")]
        public string? Slug { get; set; }
        [JsonProperty("userid")]
        public string UserId { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("cities_hosted")]
        public List<LocalGuideCityDto>? CitiesHosted { get; set; }

        [JsonProperty("registered_at")]
        public DateTime RegisteredAt { get; set; }

        [JsonProperty("distance_km")]
        public decimal DistanceKm { get; set; }
    }
}
