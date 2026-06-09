using Newtonsoft.Json;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class Community
    {
        public class AdminCommunityDto : CommunityDto
        {
            public AdminCommunityDto(dynamic details) : base((object)details)
            {
                this.IsPrivate = details.CommunityIsPrivate;
                this.InviteRequestedUsers = details.InviteRequestedUsers;
                this.InvitedCount = details.InvitedCount;
                this.JoinedCount = details.JoinedCount;
                this.ModeratorCount = details.ModeratorCount;
            }
            public bool IsPrivate { get; set; }

            public dynamic InviteRequestedUsers { get;set; }
            public long InvitedCount { get; set; }
            public long JoinedCount { get; set; }
            public long ModeratorCount { get; set; }

        }

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
                this.TotalMembers = details.TotalMembers ?? 0;
            }

            [JsonProperty("communityId")]
            public Guid CommunityId { get; set; }

            [JsonProperty("communityName")]
            public string CommunityName { get; set; } = string.Empty;

            [JsonProperty("communityDescription")]
            public string? CommunityDescription { get; set; }

            [JsonProperty("communityAvatar")]
            public string? CommunityAvatar { get; set; }

            [JsonProperty("communityBannerImage")]
            public string? CommunityBannerImage { get; set; }

            [JsonProperty("communityTags")]
            public string[]? CommunityTags { get; set; }

            [JsonProperty("communityCreatedAt")]
            public DateTime CommunityCreatedAt { get; set; }

            [JsonProperty("communityUpdatedAt")]
            public DateTime? CommunityUpdatedAt { get; set; }

            // Founder
            [JsonProperty("founderId")]
            public Guid FounderId { get; set; }

            [JsonProperty("founderUsername")]
            public string FounderUsername { get; set; } = string.Empty;

            [JsonProperty("founderAvatar")]
            public string? FounderAvatar { get; set; }


            [JsonProperty("relationshipType")]
            public string? RelationshipType { get; set; }

            // Counts
            [JsonProperty("totalMembers")]
            public long? TotalMembers { get; set; } = 0;
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
