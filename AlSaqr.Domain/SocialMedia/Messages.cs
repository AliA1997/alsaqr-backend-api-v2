using System.Text.Json.Serialization;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class Messages
    {
        public class MessageFormDto
        {
            public string MessageType { get; set; }
            public string SenderId { get; set; }
            public string SenderProfileImg { get; set; }
            public string SenderUsername { get; set; }
            public string RecipientId { get; set; }
            public string RecipientProfileImg { get; set; }
            public string RecipientUsername { get; set; }

            public string Text { get; set; }
            public string Image { get; set; }
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

            [JsonPropertyName("message_id")]
            public Guid MessageId { get; set; }

            [JsonPropertyName("sender_id")]
            public Guid SenderId { get; set; }

            [JsonPropertyName("recipient_id")]
            public Guid RecipientId { get; set; }

            [JsonPropertyName("message_content")]
            public string MessageContent { get; set; } = string.Empty;

            [JsonPropertyName("is_read")]
            public bool IsRead { get; set; }

            [JsonPropertyName("message_created_at")]
            public DateTimeOffset MessageCreatedAt { get; set; }

            [JsonPropertyName("message_updated_at")]
            public DateTimeOffset? MessageUpdatedAt { get; set; }

            // Sender
            [JsonPropertyName("sender_username")]
            public string SenderUsername { get; set; } = string.Empty;

            [JsonPropertyName("sender_avatar")]
            public string? SenderAvatar { get; set; }

            // Recipient
            [JsonPropertyName("recipient_username")]
            public string RecipientUsername { get; set; } = string.Empty;

            [JsonPropertyName("recipient_avatar")]
            public string? RecipientAvatar { get; set; }
        }
    }
}
