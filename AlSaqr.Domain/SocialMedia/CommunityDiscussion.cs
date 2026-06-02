using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using static AlSaqr.Domain.SocialMedia.User;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class CommunityDiscussion
    {
        public class CreateCommunityDiscussionMessageForm
        {
            public Guid CreatorId { get; set; }
            public string Content { get; set; }
            public string Media { get; set; }
        }
        public class CreateCommunityDiscussionForm
        {
            public string Name { get; set; }
            public string? Description { get; set; }
            public string IsPrivate { get; set; }
            public Guid[] UsersAdded { get; set; }
        }

        public class CommunityDiscussionInviteConfirmationDto
        {
            public string Username { get; set; }
            public string Email { get; set; }
        }

        public class AcceptOrDenyCommunityDiscussionInviteConfirmationDto
        {
            public bool Accept { get; set; }
            public bool Deny { get; set; }
        }

        public class CommunityDiscussionMessageDto
        {
            public CommunityDiscussionMessageDto(dynamic details)
            {
                this.Id = details.Id;
                this.UserId = details.CreatorId;
                this.CommunityDiscussionId = details.CommunityDiscussionId;
                this.MessageText = details.Content;
                this.Image = details.Media;
                this.CreatedAt = details.CreatedAt;
            }
            [JsonProperty("id")]
            public Guid Id { get; set; }

            [JsonProperty("userId")]
            public Guid UserId { get; set; }
            [JsonProperty("communityDiscussionId")]
            public Guid CommunityDiscussionId { get; set; }
            [JsonProperty("messageText")]
            public string MessageText { get; set; }
            [JsonProperty("image")]
            public string Image { get; set; }
            [JsonProperty("createdAt")]
            public DateTime CreatedAt { get; set; }
        }

        public class CommunityDiscussionDto
        {
            public CommunityDiscussionDto(dynamic details)
            {
                this.DiscussionId = details.DiscussionId;
                this.CommunityId = details.CommunityId;
                this.DiscussionTitle = details.DiscussionTitle;
                this.DiscussionContent = details.DiscussionContent;
                this.DiscussionCreatedAt = details.DiscussionCreatedAt;
                this.DiscussionUpdatedAt = details.DiscussionUpdatedAt;
                this.CreatorId = details.CreatorId;
                this.CreatorUsername = details.CreatorUsername;
                this.CreatorAvatar = details.CreatorAvatar;
                this.UserId = details.UserId;
                this.UserRole = details.UserRole;
                this.UserJoinedAt = details.UserJoinedAt;
                this.RelationshipType = details.RelationshipType;
                this.JoinedUsers = details.JoinedUsers ?? new Dictionary<string, object>[] { };
                this.ModeratorUsers = details.ModeratorUsers ?? new Dictionary<string, object>[] { };
                this.InvitedUsers = details.InvitedUsers ?? new Dictionary<string, object>[] { };
                this.RequestedUsers = details.RequestedUsers ?? new Dictionary<string, object>[] { };
                this.MemberCount = details.MemberCount ?? 0;
                this.ModeratorCount = details.ModeratorCount ?? 0;
                this.InvitedCount = details.InvitedCount ?? 0;
                this.RequestedCount = details.RequestedCount ?? 0;
                this.TotalMemberCount = details.TotalMemberCount ?? 0;
                this.LastMessageId = details.LastMessageId;
                this.LastMessageCreatorId = details.LastMessageCreatorId;
                this.LastMessageContent = details.LastMessageContent;
                this.LastMessageAt = details.LastMessageAt;
            }

            [JsonPropertyName("communityDiscussionId")]
            public Guid DiscussionId { get; set; }

            [JsonPropertyName("communityId")]
            public Guid CommunityId { get; set; }

            [JsonPropertyName("communityDiscussionTitle")]
            public string DiscussionTitle { get; set; } = string.Empty;

            [JsonPropertyName("communityDiscussionContent")]
            public string DiscussionContent { get; set; } = string.Empty;

            [JsonPropertyName("communityDiscussionCreatedAt")]
            public DateTime DiscussionCreatedAt { get; set; }

            [JsonPropertyName("communityDiscussionUpdatedAt")]
            public DateTime? DiscussionUpdatedAt { get; set; }

            // Creator
            [JsonPropertyName("creatorId")]
            public Guid CreatorId { get; set; }

            [JsonPropertyName("creatorUsername")]
            public string CreatorUsername { get; set; } = string.Empty;

            [JsonPropertyName("creatorAvatar")]
            public string? CreatorAvatar { get; set; }

            // Requesting user context
            [JsonPropertyName("userId")]
            public Guid? UserId { get; set; }

            [JsonPropertyName("userRole")]
            public string? UserRole { get; set; }

            [JsonPropertyName("userJoinedAt")]
            public DateTime? UserJoinedAt { get; set; }

            [JsonPropertyName("relationshipType")]
            public string? RelationshipType { get; set; }

            // Aggregated member lists (JSONB → string; deserialize downstream)
            [JsonPropertyName("joinedUsers")]
            public IDictionary<string, object>[]? JoinedUsers { get; set; }

            [JsonPropertyName("moderatorUsers")]
            public IDictionary<string, object>[]? ModeratorUsers { get; set; }
            [JsonPropertyName("invitedUsers")]
            public IDictionary<string, object>[]? InvitedUsers { get; set; }

            [JsonPropertyName("requestedUsers")]
            public IDictionary<string, object>[]? RequestedUsers { get; set; }

            // Counts
            [JsonPropertyName("memberCount")]
            public long MemberCount { get; set; } = 0;

            [JsonPropertyName("moderatorCount")]
            public long ModeratorCount { get; set; } = 0;
            [JsonPropertyName("invitedCount")]
            public long InvitedCount { get; set; } = 0;

            [JsonPropertyName("requestedCount")]
            public long RequestedCount { get; set; } = 0;

            [JsonPropertyName("totalMemberCount")]
            public long TotalMemberCount { get; set; }

            // Latest message preview
            [JsonPropertyName("lastMessageId")]
            public Guid? LastMessageId { get; set; }

            [JsonPropertyName("lastMessageCreatorId")]
            public Guid? LastMessageCreatorId { get; set; }

            [JsonPropertyName("lastMessageContent")]
            public string? LastMessageContent { get; set; }

            [JsonPropertyName("lastMessageAt")]
            public DateTime? LastMessageAt { get; set; }
        }

        public class InviteUserSummaryDto
        {
            [JsonProperty("id")]
            public Guid Id { get; set; }
            [JsonProperty("userId")]
            public Guid UserId { get; set; }

            [JsonProperty("username")]
            public string Username { get; set; } = string.Empty;

            [JsonProperty("avatar")]
            public string? Avatar { get; set; }

        }

        /// <summary>
        /// Admin view of a community discussion. Mirrors the shape returned by the
        /// original Neo4j query: the discussion, the founder, whether the caller is
        /// the founder, member counts by relationship type, and the list of users
        /// who have requested to join.
        /// </summary>
        public class AdminCommunityDiscussionInfoDto
        {
            public Guid DiscussionId { get; set; }
            public Guid CommunityId { get; set; }
            public string? Title { get; set; }
            public string? Content { get; set; }
            public DateTime CreatedAt { get; set; }

            public bool IsFounder { get; set; }
            public UserSummaryDto? Founder { get; set; }

            public long InvitedCount { get; set; }
            public long JoinedCount { get; set; }
            public long ModeratorCount { get; set; }
            public long RequestedCount { get; set; }

            public List<InviteUserSummaryDto> InviteRequestedUsers { get; set; } = new();

            public AdminCommunityDiscussionInfoDto() { }

            public AdminCommunityDiscussionInfoDto(dynamic details, bool isFounder, List<dynamic> inviteRequestedUsers)
            {
                DiscussionId = details.DiscussionId;
                CommunityId = details.CommunityId;
                Title = details.DiscussionTitle;
                Content = details.DiscussionContent;
                CreatedAt = details.DiscussionCreatedAt;

                IsFounder = isFounder;
                Founder = new UserSummaryDto
                {
                    UserId = details.CreatorId,
                    Username = details.FounderUsername,
                    Avatar = details.FounderAvatar
                };

                InvitedCount = details.InvitedCount ?? 0;
                JoinedCount = details.JoinedCount ?? 0;
                ModeratorCount = details.ModeratorCount ?? 0;
                RequestedCount = details.RequestedCount ?? 0;

                InviteRequestedUsers = inviteRequestedUsers
                    .Select(u => new InviteUserSummaryDto
                    {
                        Id = u.Id,
                        UserId = u.UserId,
                        Username = u.Username,
                        Avatar = u.Avatar
                    })
                    .ToList();
            }
        }
    }
}
