using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
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

        public class MessageHistoryDto
        {
            public MessageHistoryDto(dynamic details)
            {
                this.Id = details.Id;
                this.ViewerId = details.ViewerId;
                this.ReceiverId = details.ReceiverId;
                this.ReceiverUsername = details.ReceiverUsername;
                this.ReceiverProfileImage = details.ReceiverProfileImage;
                this.MessageCount = details.MessageCount;
                this.LastMessageDate = details.LastMessageDate;
            }

            [JsonPropertyName("id")]
            public Guid Id { get; set; }

            [JsonPropertyName("viewerId")]
            public Guid ViewerId { get; set; }

            [JsonPropertyName("receiverId")]
            public Guid ReceiverId { get; set; }

            [JsonPropertyName("receiverProfileImage")]
            public string? ReceiverProfileImage { get; set; }

            [JsonPropertyName("receiverUsername")]
            public string? ReceiverUsername { get; set; } = string.Empty;

            [JsonPropertyName("messageCount")]
            public long MessageCount { get; set; }

            [Column("lastMessageDate")]
            public DateTime LastMessageDate { get; set; }
        }

        public class MessageDto
        {
            public MessageDto(dynamic details)
            {
                this.MessageId = details.MessageId;
                this.SenderId = details.SenderId;
                this.RecipientId = details.RecipientId;
                this.MessageContent = details.MessageContent;
                this.MessageMedia = details.MessageMedia;
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
            [JsonPropertyName("messageMedia")]
            public string MessageMedia { get; set; } = string.Empty;

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
