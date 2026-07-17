using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// Output DTO for a member belonging to a group. Mapped from the vw_group_members
    /// read model; never reused as an input model.
    /// </summary>
    public class GroupMemberDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("groupId")]
        public Guid GroupId { get; set; }

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("hobbies")]
        public string[] Hobbies { get; set; } = Array.Empty<string>();

        [JsonPropertyName("isGroupOrganizer")]
        public bool IsGroupOrganizer { get; set; }

        [JsonPropertyName("isLocalGuide")]
        public bool IsLocalGuide { get; set; }

        [JsonPropertyName("joinedAt")]
        public DateTime JoinedAt { get; set; }

        public GroupMemberDto() { }

        // Mapped from VwGroupMembers. Takes a dynamic view because AlSaqr.Domain
        // does not (and must not) reference AlSaqr.Data entities — mirrors EventMemberDto.
        public GroupMemberDto(dynamic view)
        {
            Id = view.Id;
            GroupId = view.GroupId;
            UserId = view.UserId;
            Username = view.Username;
            Avatar = view.Avatar;
            Hobbies = view.Hobbies ?? Array.Empty<string>();
            IsGroupOrganizer = view.IsGroupOrganizer;
            IsLocalGuide = view.IsLocalGuide;
            JoinedAt = view.CreatedAt;
        }
    }
}
