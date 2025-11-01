using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Domain.Meetup
{
    public class LocalGuideDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("userid")]
        public string UserId { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("state_or_province")]
        public string StateOrProvince { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("latitude")]
        public decimal Latitude { get; set; }

        [JsonProperty("longitude")]
        public decimal Longitude { get; set; }

        [JsonProperty("registered_at")]
        public DateTime RegisteredAt { get; set; }

        [JsonProperty("distance_km")]
        public decimal DistanceKm { get; set; }
    }
}
