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
                this.JoinedUsers = details.JoinedUsers;
                this.ModeratorUsers = details.ModeratorUsers;
                this.MemberCount = details.MemberCount;
                this.ModeratorCount = details.ModeratorCount;
                this.TotalMemberCount = details.TotalMemberCount;
                this.LastMessageId = details.LastMessageId;
                this.LastMessageCreatorId = details.LastMessageCreatorId;
                this.LastMessageContent = details.LastMessageContent;
                this.LastMessageAt = details.LastMessageAt;
            }

            [JsonProperty("communityDiscussionId")]
            public Guid DiscussionId { get; set; }

            [JsonProperty("communityId")]
            public Guid CommunityId { get; set; }

            [JsonProperty("communityDiscussionTitle")]
            public string DiscussionTitle { get; set; } = string.Empty;

            [JsonProperty("communityDiscussionContent")]
            public string DiscussionContent { get; set; } = string.Empty;

            [JsonProperty("communityDiscussionCreatedAt")]
            public DateTime DiscussionCreatedAt { get; set; }

            [JsonProperty("communityDiscussionUpdatedAt")]
            public DateTime? DiscussionUpdatedAt { get; set; }

            // Creator
            [Column("creatorId")]
            public Guid CreatorId { get; set; }

            [Column("creatorUsername")]
            public string CreatorUsername { get; set; } = string.Empty;

            [Column("creatorAvatar")]
            public string? CreatorAvatar { get; set; }

            // Requesting user context
            [Column("userId")]
            public Guid? UserId { get; set; }

            [Column("userRole")]
            public string? UserRole { get; set; }

            [Column("userJoinedAt")]
            public DateTime? UserJoinedAt { get; set; }

            [Column("relationshipType")]
            public string? RelationshipType { get; set; }

            // Aggregated member lists (JSONB → string; deserialize downstream)
            [Column("joinedUsers")]
            public string JoinedUsers { get; set; } = "[]";

            [Column("moderatorUsers")]
            public string ModeratorUsers { get; set; } = "[]";

            // Counts
            [Column("memberCount")]
            public long MemberCount { get; set; }

            [Column("moderatorCount")]
            public long ModeratorCount { get; set; }

            [Column("totalMemberCount")]
            public long TotalMemberCount { get; set; }

            // Latest message preview
            [Column("lastMessageId")]
            public Guid? LastMessageId { get; set; }

            [Column("lastMessageCreatorId")]
            public Guid? LastMessageCreatorId { get; set; }

            [Column("lastMessageContent")]
            public string? LastMessageContent { get; set; }

            [Column("lastMessageAt")]
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

            public int InvitedCount { get; set; }
            public int JoinedCount { get; set; }
            public int RequestedCount { get; set; }

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
                    Username = details.FounderUsername,
                    Avatar = details.FounderAvatar
                };

                InvitedCount = details.InvitedCount;
                JoinedCount = details.JoinedCount;
                RequestedCount = details.RequestedCount;

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
