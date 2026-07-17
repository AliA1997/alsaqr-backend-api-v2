using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// Output DTO for a member attending an event. Mapped from the vw_event_members
    /// read model; never reused as an input model.
    /// </summary>
    public class EventMemberDto
    {
        [JsonPropertyName("eventId")]
        public Guid EventId { get; set; }

        [JsonPropertyName("groupId")]
        public Guid? GroupId { get; set; }

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("hobbies")]
        public string[] Hobbies { get; set; } = Array.Empty<string>();

        [JsonPropertyName("isEventOrganizer")]
        public bool IsEventOrganizer { get; set; }

        [JsonPropertyName("isLocalGuide")]
        public bool IsLocalGuide { get; set; }

        [JsonPropertyName("joinedAt")]
        public DateTime JoinedAt { get; set; }

        public EventMemberDto() { }

        // Mapped from VwEventMembers. Takes a dynamic view because AlSaqr.Domain
        // does not (and must not) reference AlSaqr.Data entities — mirrors AttendedEventDto.
        public EventMemberDto(dynamic view)
        {
            EventId = view.EventId;
            GroupId = view.GroupId;
            UserId = view.UserId;
            Username = view.Username;
            Avatar = view.UserAvatar;
            Hobbies = view.UserHobbies ?? Array.Empty<string>();
            IsEventOrganizer = view.IsEventOrganizer;
            IsLocalGuide = view.IsLocalGuide;
            JoinedAt = view.CreatedAt;
        }
    }
}
