
using AlSaqr.Data.Entities;
using Newtonsoft.Json;

namespace AlSaqr.Domain.Meetup
{
    public class LocalGuideDetailsDto
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
        public List<LocalGuideCityDto> CitiesHosted { get; set; }

        [JsonProperty("registered_at")]
        public DateTime RegisteredAt { get; set; }

        [JsonProperty("distance_km")]
        public decimal DistanceKm { get; set; }
        [JsonProperty("userInfo")]
        public dynamic? UserInfo { get; set; }
    }
}
