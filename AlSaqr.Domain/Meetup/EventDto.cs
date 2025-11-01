using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace AlSaqr.Domain.Meetup
{
    public class EventDto
    {
        [JsonProperty("id")]
        public long? Id { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("description")]
        public string? Description { get; set; }
        [JsonProperty("images")]
        public IDictionary<string, object>[]? Images { get; set; }
        [JsonProperty("group_id")]
        public long? GroupId { get; set; }
        [JsonProperty("group_name")]
        public string? GroupName { get; set; }

        [JsonProperty("cities_hosted")]
        public IDictionary<string, object>[]? CitiesHosted { get; set; }
        [JsonProperty("distance_km")]
        public decimal? DistanceKm { get; set; }
    }
}
