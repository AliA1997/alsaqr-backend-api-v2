using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// Output DTO for an event a user attended (spec EA-2). Mapped from the
    /// vw_event_attendees read model; never reused as an input model.
    /// </summary>
    public class AttendedEventDto
    {
        [JsonPropertyName("eventId")]
        public Guid EventId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("images")]
        public string[] Images { get; set; } = Array.Empty<string>();

        // Host group identity (id + name).
        [JsonPropertyName("groupId")]
        public Guid GroupId { get; set; }

        [JsonPropertyName("groupName")]
        public string? GroupName { get; set; }

        // Each city carries id, name, latitude, longitude. Empty list when the
        // event has no hosted cities — never null/absent (spec EA-2 acceptance).
        [JsonPropertyName("citiesHosted")]
        public IDictionary<string, object>[] CitiesHosted { get; set; } = Array.Empty<IDictionary<string, object>>();

        [JsonPropertyName("joinedAt")]
        public DateTime JoinedAt { get; set; }

        public AttendedEventDto() { }

        // Mapped from VwEventAttendees. Takes a dynamic view because AlSaqr.Domain
        // does not (and must not) reference AlSaqr.Data entities — mirrors ProfilePostDto.
        public AttendedEventDto(dynamic view)
        {
            EventId = view.EventId;
            Name = view.EventName;
            Slug = view.EventSlug;
            Description = view.EventDescription;
            Images = view.EventImages ?? Array.Empty<string>();
            GroupId = view.GroupId;
            GroupName = view.GroupName;
            CitiesHosted = view.CitiesHosted ?? Array.Empty<IDictionary<string, object>>();
            JoinedAt = view.JoinedAt;
        }
    }
}
