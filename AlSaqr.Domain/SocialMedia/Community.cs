using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class Community
    {
        public class CommunityDto
        {
            public CommunityDto(dynamic details) 
            { 
                this.CommunityId = details.CommunityId;
                this.CommunityName = details.CommunityName;
                this.CommunityDescription = details.CommunityDescription;
                this.CommunityAvatar = details.CommunityAvatar;
                this.CommunityBannerImage = details.CommunityBannerImage;
                this.CommunityTags = details.CommunityTags;
                this.CommunityCreatedAt = details.CommunityCreatedAt;
                this.CommunityUpdatedAt = details.CommunityUpdatedAt;
                this.FounderId = details.FounderId;
                this.FounderUsername = details.FounderUsername;
                this.FounderAvatar = details.FounderAvatar;
                this.UserId = details.UserId;
                this.UserRole = details.UserRole;
                this.UserJoinedAt = details.UserJoinedAt;
                this.RelationshipType = details.RelationshipType;
                this.TotalMembers = details.TotalMembers;
            }

            [JsonProperty("community_id")]
            public Guid CommunityId { get; set; }

            [JsonProperty("community_name")]
            public string CommunityName { get; set; } = string.Empty;

            [JsonProperty("community_description")]
            public string? CommunityDescription { get; set; }

            [JsonProperty("community_avatar")]
            public string? CommunityAvatar { get; set; }

            [JsonProperty("community_banner_image")]
            public string? CommunityBannerImage { get; set; }

            [JsonProperty("community_tags")]
            public string[]? CommunityTags { get; set; }

            [JsonProperty("community_created_at")]
            public DateTime CommunityCreatedAt { get; set; }

            [JsonProperty("community_updated_at")]
            public DateTime? CommunityUpdatedAt { get; set; }

            // Founder
            [JsonProperty("founder_id")]
            public Guid FounderId { get; set; }

            [JsonProperty("founder_username")]
            public string FounderUsername { get; set; } = string.Empty;

            [JsonProperty("founder_avatar")]
            public string? FounderAvatar { get; set; }

            // Requesting user context
            [JsonProperty("user_id")]
            public Guid? UserId { get; set; }

            [JsonProperty("user_role")]
            public string? UserRole { get; set; }

            [JsonProperty("user_joined_at")]
            public DateTime? UserJoinedAt { get; set; }

            [JsonProperty("relationship_type")]
            public string? RelationshipType { get; set; }

            // Counts
            [JsonProperty("total_members")]
            public long TotalMembers { get; set; }
        }

        public class CreateCommunityFormDto: CreateCommunityForm
        {
            public Guid[] UsersAdded { get; set; } = new Guid[] { };
            public Guid[] PostsAdded { get; set; } = new Guid[] { };
        }
        public class CreateCommunityForm 
        { 
            public string Name { get; set; }
            public string? Description { get; set; }
            public string AvatarOrBannerImage { get; set; }
            public string IsPrivate { get; set; }
            public string[] Tags { get; set; }
            public object[] UsersAdded { get; set; }
            public object[] PostsAdded { get; set; }
        }

        public class UpdateCommunityForm
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string? Description { get; set; }
            public string Avatar { get; set; }
            public string IsPrivate { get; set; }
            public string[] Tags { get; set; }
        }

        public class CommunityInviteConfirmationDto 
        { 
            public string Username { get; set; }
            public string Email { get; set; }
        }

        public class AcceptOrDenyCommunityInviteConfirmationDto
        {
            public bool Accept { get; set; }
            public bool Deny { get; set; }
        }

    }
}
