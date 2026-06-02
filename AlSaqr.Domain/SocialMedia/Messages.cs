using System.Text.Json.Serialization;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class Messages
    {
        public class MessageFormDto
        {
            public Guid SenderId { get; set; }
            public string SenderProfileImg { get; set; }
            public string SenderUsername { get; set; }
            public Guid RecipientId { get; set; }
            public string RecipientProfileImg { get; set; }
            public string RecipientUsername { get; set; }

            public string Text { get; set; }
            public string? Image { get; set; }
        }


        public class MessageDto
        {
            public MessageDto(dynamic details)
            {
                this.MessageId = details.MessageId;
                this.SenderId = details.SenderId;
                this.RecipientId = details.RecipientId;
                this.MessageContent = details.MessageContent;
                this.IsRead = details.IsRead;
                this.MessageCreatedAt = details.MessageCreatedAt;
                this.MessageUpdatedAt = details.MessageUpdatedAt;
                this.SenderUsername = details.SenderUsername;
                this.SenderAvatar = details.SenderAvatar;
                this.RecipientUsername = details.RecipientUsername;
                this.RecipientAvatar = details.RecipientAvatar;
            }

            [JsonPropertyName("messageId")]
            public Guid MessageId { get; set; }

            [JsonPropertyName("senderId")]
            public Guid SenderId { get; set; }

            [JsonPropertyName("recipientId")]
            public Guid RecipientId { get; set; }

            [JsonPropertyName("messageContent")]
            public string MessageContent { get; set; } = string.Empty;

            [JsonPropertyName("isRead")]
            public bool IsRead { get; set; }

            [JsonPropertyName("messageCreatedAt")]
            public DateTimeOffset MessageCreatedAt { get; set; }

            [JsonPropertyName("messageUpdatedAt")]
            public DateTimeOffset? MessageUpdatedAt { get; set; }

            // Sender
            [JsonPropertyName("senderUsername")]
            public string SenderUsername { get; set; } = string.Empty;

            [JsonPropertyName("senderAvatar")]
            public string? SenderAvatar { get; set; }

            // Recipient
            [JsonPropertyName("recipientUsername")]
            public string RecipientUsername { get; set; } = string.Empty;

            [JsonPropertyName("recipientAvatar")]
            public string? RecipientAvatar { get; set; }
        }
    }
}
