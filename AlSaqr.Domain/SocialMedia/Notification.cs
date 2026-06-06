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

        [JsonPropertyName("notificationId")]
        public Guid NotificationId { get; set; }

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("notificationType")]
        public string NotificationType { get; set; } = string.Empty;

        [JsonPropertyName("notificationMessage")]
        public string? NotificationMessage { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        [JsonPropertyName("notificationCreatedAt")]
        public DateTime NotificationCreatedAt { get; set; }

        [JsonPropertyName("notificationUpdatedAt")]
        public DateTime? NotificationUpdatedAt { get; set; }



        [JsonPropertyName("relatedUserId")]
        public Guid? RelatedUserId { get; set; }

        [JsonPropertyName("postId")]
        public Guid? PostId { get; set; }

        [JsonPropertyName("communityId")]
        public Guid? CommunityId { get; set; }

        [JsonPropertyName("communityDiscussionId")]
        public Guid? CommunityDiscussionId { get; set; }

        [JsonPropertyName("communityDiscussionMessageId")]
        public Guid? CommunityDiscussionMessageId { get; set; }

        [JsonPropertyName("listId")]
        public Guid? ListId { get; set; }
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
