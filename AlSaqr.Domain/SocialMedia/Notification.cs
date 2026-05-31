using System.Text.Json.Serialization;

namespace AlSaqr.Domain.SocialMedia
{
    public sealed class NotificationDto
    {
        public NotificationDto(dynamic details)
        {
            this.NotificationId = details.NotificationId;
            this.UserId = details.UserId;
            this.NotificationType = details.NotificationType;
            this.NotificationMessage = details.NotificationMessage;
            this.IsRead = details.IsRead;
            this.NotificationCreatedAt = details.NotificationCreatedAt;
            this.NotificationUpdatedAt = details.NotificationUpdatedAt;
            //this.TriggeredBy = details.TriggeredBy == null ? null : new NotificationActorDto(details.TriggeredBy);
            this.PostId = details.PostId;
            this.CommunityId = details.CommunityId;
            this.CommunityDiscussionId = details.CommunityDiscussionId;
        }

        [JsonPropertyName("notification_id")]
        public Guid NotificationId { get; set; }

        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }

        [JsonPropertyName("notification_type")]
        public string NotificationType { get; set; } = string.Empty;

        [JsonPropertyName("notification_message")]
        public string? NotificationMessage { get; set; }

        [JsonPropertyName("is_read")]
        public bool IsRead { get; set; }

        [JsonPropertyName("notification_created_at")]
        public DateTime NotificationCreatedAt { get; set; }

        [JsonPropertyName("notification_updated_at")]
        public DateTime? NotificationUpdatedAt { get; set; }

        //[JsonPropertyName("triggered_by")]
        //public NotificationActorDto? TriggeredBy { get; set; }

        [JsonPropertyName("post_id")]
        public Guid? PostId { get; set; }

        [JsonPropertyName("community_id")]
        public Guid? CommunityId { get; set; }

        [JsonPropertyName("community_discussion_id")]
        public Guid? CommunityDiscussionId { get; set; }
    }

    /// <summary>
    /// The user who triggered the notification.
    /// </summary>
    public sealed class NotificationActorDto
    {
        [JsonPropertyName("user_id")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }
}
