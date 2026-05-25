using Newtonsoft.Json;


namespace AlSaqr.Domain.Meetup
{
    public class EventDto
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }
        [JsonProperty("slug")]
        public string? Slug { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("description")]
        public string? Description { get; set; }
        [JsonProperty("images")]
        public string[]? Images { get; set; }
        [JsonProperty("group_id")]
        public Guid? GroupId { get; set; }
        [JsonProperty("group_name")]
        public string? GroupName { get; set; }

        [JsonProperty("cities_hosted")]
        public IDictionary<string, object>[]? CitiesHosted { get; set; }
        [JsonProperty("distance_km")]
        public decimal? DistanceKm { get; set; }
    }
}
