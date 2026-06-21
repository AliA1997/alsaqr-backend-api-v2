using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// Output DTO for a group a user has joined (spec GJ-2). Mapped from the
    /// vw_group_attendees read model; never reused as an input model.
    /// </summary>
    public class JoinedGroupDto
    {
        [JsonPropertyName("groupId")]
        public Guid GroupId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("images")]
        public string[] Images { get; set; } = Array.Empty<string>();

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("hqCityId")]
        public Guid HqCityId { get; set; }

        [JsonPropertyName("hqCity")]
        public string? HqCity { get; set; }

        [JsonPropertyName("hqCountry")]
        public string? HqCountry { get; set; }

        [JsonPropertyName("hqLatitude")]
        public decimal? HqLatitude { get; set; }

        [JsonPropertyName("hqLongitude")]
        public decimal? HqLongitude { get; set; }

        // Each topic carries its id and name. Empty list when the group has no
        // topics — never null/absent (spec GJ-2 acceptance).
        [JsonPropertyName("topics")]
        public IDictionary<string, object>[] Topics { get; set; } = Array.Empty<IDictionary<string, object>>();

        [JsonPropertyName("joinedAt")]
        public DateTime JoinedAt { get; set; }

        public JoinedGroupDto() { }

        // Mapped from VwGroupAttendees. Takes a dynamic view because AlSaqr.Domain
        // does not (and must not) reference AlSaqr.Data entities — mirrors ProfilePostDto.
        public JoinedGroupDto(dynamic view)
        {
            GroupId = view.GroupId;
            Name = view.GroupName;
            Description = view.GroupDescription;
            Images = view.GroupImages ?? Array.Empty<string>();
            Slug = view.GroupSlug;
            HqCityId = view.HqCityId;
            HqCity = view.HqCity;
            HqCountry = view.HqCountry;
            HqLatitude = view.HqLatitude;
            HqLongitude = view.HqLongitude;
            Topics = view.Topics ?? Array.Empty<IDictionary<string, object>>();
            JoinedAt = view.JoinedAt;
        }
    }
}
