using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace  AlSaqr.Domain.SocialMedia
{
    public static class List
    {


        /// <summary>
        /// Full list detail returned from the database.
        /// </summary>
        public sealed class ListDto
        {
            public ListDto(dynamic details)
            {
                this.ListId = details.ListId;
                this.ListName = details.ListName;
                this.ListDescription = details.ListDescription;
                this.ListAvatar = details.ListAvatar;
                this.ListBannerImage = details.ListBannerImage;
                this.ListTags = details.ListTags;
                this.ListCreatedAt = details.ListCreatedAt;
                this.ListUpdatedAt = details.ListUpdatedAt;
                this.Owner = new ListOwnerDto()
                {
                    OwnerUsername = details.OwnerUsername,
                    OwnerAvatar = details.OwnerAvatar
                };
                this.ItemCounts = new ListItemCountsDto()
                {
                    TotalItems = details.TotalItems,
                    UserItems = details.UserItems,
                    PostItems = details.PostItems,
                    CommunityItems = details.CommunityItems,
                    DiscussionItems = details.DiscussionItems,
                    MessageItems = details.MessageItems
                };
                this.LastSavedItem = new LastSavedItemPreviewDto()
                {
                    LastItemId = details.LastItemId,
                    LastItemType = details.LastItemType,
                    LastSavedAt = details.LastSavedAt
                };

            }

            [JsonPropertyName("listId")]
            public Guid ListId { get; set; }

            [JsonPropertyName("listName")]
            public string ListName { get; set; } = string.Empty;

            [JsonPropertyName("listDescription")]
            public string? ListDescription { get; set; }

            [JsonPropertyName("listAvatar")]
            public string? ListAvatar { get; set; }

            [JsonPropertyName("listBannerImage")]
            public string? ListBannerImage { get; set; }

            [JsonPropertyName("listTags")]
            public string[]? ListTags { get; set; }

            [JsonPropertyName("listCreatedAt")]
            public DateTime ListCreatedAt { get; set; }

            [JsonPropertyName("listUpdatedAt")]
            public DateTime? ListUpdatedAt { get; set; }

            [JsonPropertyName("owner")]
            public ListOwnerDto Owner { get; set; } = new();

            [JsonPropertyName("itemCounts")]
            public ListItemCountsDto ItemCounts { get; set; } = new();

            [JsonPropertyName("lastSavedItem")]
            public LastSavedItemPreviewDto? LastSavedItem { get; set; }
        }

        public sealed class ListOwnerDto
        {
            [JsonPropertyName("userId")]
            public Guid UserId { get; set; }

            [JsonPropertyName("ownerUsername")]
            public string OwnerUsername { get; set; } = string.Empty;

            [JsonPropertyName("ownerAvatar")]
            public string? OwnerAvatar { get; set; }
        }

        public sealed class ListItemCountsDto
        {
            [JsonPropertyName("totalItems")]
            public long TotalItems { get; set; }

            [JsonPropertyName("userItems")]
            public long UserItems { get; set; }

            [JsonPropertyName("postItems")]
            public long PostItems { get; set; }

            [JsonPropertyName("communityItems")]
            public long CommunityItems { get; set; }

            [JsonPropertyName("discussionItems")]
            public long DiscussionItems { get; set; }

            [JsonPropertyName("messageItems")]
            public long MessageItems { get; set; }
        }

        public sealed class LastSavedItemPreviewDto
        {
            [JsonPropertyName("lastItemId")]
            public Guid? LastItemId { get; set; }

            [JsonPropertyName("lastItemType")]
            public string? LastItemType { get; set; }

            [JsonPropertyName("lastSavedAt")]
            public DateTimeOffset? LastSavedAt { get; set; }
        }

        public class CreateListFormDto : CreateListForm
        {
            public Guid[] UsersAdded { get; set; }
            public Guid[] PostsAdded { get; set; }
        }
        public class CreateListForm
        {
            public string Name { get; set; }
            public string AvatarOrBannerImage { get; set; }
            public string IsPrivate { get; set; }
            public string[] Tags { get; set; }
            public object[] UsersAdded { get; set; }
            public object[] PostsAdded { get; set; }
        }

        public class SaveItemToListDto
        {
            public string RelatedEntityId { get; set; }
            public string Type { get; set; }
        }

        public sealed class ListItemDto
        {
            [JsonProperty("listItemId")]
            public Guid ListItemId { get; set; }

            [JsonProperty("listId")]
            public Guid ListId { get; set; }

            [JsonProperty("itemType")]
            public string ItemType { get; set; } = string.Empty;

            [JsonProperty("savedAt")]
            public DateTime SavedAt { get; set; }

            // Post
            [JsonProperty("postId")]
            public Guid? PostId { get; set; }

            [JsonProperty("postContent")]
            public string? PostContent { get; set; }

            [JsonProperty("postType")]
            public string? PostType { get; set; }

            [JsonProperty("postTags")]
            public string[]? PostTags { get; set; }

            [JsonProperty("postCreatedAt")]
            public DateTime? PostCreatedAt { get; set; }

            [JsonProperty("postUsername")]
            public string? PostUsername { get; set; }

            [JsonProperty("postAvatar")]
            public string? PostAvatar { get; set; }

            // Community
            [JsonProperty("communityId")]
            public Guid? CommunityId { get; set; }

            [JsonProperty("communityName")]
            public string? CommunityName { get; set; }

            [JsonProperty("communityDescription")]
            public string? CommunityDescription { get; set; }

            [JsonProperty("communityAvatar")]
            public string? CommunityAvatar { get; set; }

            [JsonProperty("communityBannerImage")]
            public string? CommunityBannerImage { get; set; }

            [JsonProperty("communityTags")]
            public string[]? CommunityTags { get; set; }

            [JsonProperty("communityFounderUsername")]
            public string? CommunityFounderUsername { get; set; }

            [JsonProperty("communityFounderAvatar")]
            public string? CommunityFounderAvatar { get; set; }

            // Saved user
            [JsonProperty("savedUserId")]
            public Guid? SavedUserId { get; set; }

            [JsonProperty("savedUserUsername")]
            public string? SavedUserUsername { get; set; }

            [JsonProperty("savedUserAvatar")]
            public string? SavedUserAvatar { get; set; }

            [JsonProperty("savedUserBio")]
            public string? SavedUserBio { get; set; }

            // Community discussion
            [JsonProperty("communityDiscussionId")]
            public Guid? CommunityDiscussionId { get; set; }

            [JsonProperty("communityDiscussionTitle")]
            public string? CommunityDiscussionTitle { get; set; }

            [JsonProperty("communityDiscussionContent")]
            public string? CommunityDiscussionContent { get; set; }

            [JsonProperty("communityDiscussionCreatedAt")]
            public DateTime? CommunityDiscussionCreatedAt { get; set; }

            [JsonProperty("communityDiscussionUsername")]
            public string? CommunityDiscussionUsername { get; set; }

            [JsonProperty("communityDiscussionAvatar")]
            public string? CommunityDiscussionAvatar { get; set; }

            // Community discussion message
            [JsonProperty("communityDiscussionMessageId")]
            public Guid? CommunityDiscussionMessageId { get; set; }

            [JsonProperty("communityDiscussionMessageContent")]
            public string? CommunityDiscussionMessageContent { get; set; }

            [JsonProperty("communityDiscussionMessageMedia")]
            public string? CommunityDiscussionMessageMedia { get; set; }

            [JsonProperty("communityDiscussionMessageCreatedAt")]
            public DateTime? CommunityDiscussionMessageCreatedAt { get; set; }

            [JsonProperty("communityDiscussionMessageUsername")]
            public string? CommunityDiscussionMessageUsername { get; set; }

            [JsonProperty("communityDiscussionMessageAvatar")]
            public string? CommunityDiscussionMessageAvatar { get; set; }

            public ListItemDto() { }

            public ListItemDto(dynamic view)
            {
                ListItemId = view.ListItemId;
                ListId = view.ListId;
                ItemType = view.ItemType;
                SavedAt = view.SavedAt;

                // Post
                PostId = view.PostId;
                PostContent = view.PostContent;
                PostType = view.PostType;
                PostTags = view.PostTags;
                PostCreatedAt = view.PostCreatedAt;
                PostUsername = view.PostUsername;
                PostAvatar = view.PostAvatar;

                // Community
                CommunityId = view.CommunityId;
                CommunityName = view.CommunityName;
                CommunityDescription = view.CommunityDescription;
                CommunityAvatar = view.CommunityAvatar;
                CommunityBannerImage = view.CommunityBannerImage;
                CommunityTags = view.CommunityTags;
                CommunityFounderUsername = view.CommunityFounderUsername;
                CommunityFounderAvatar = view.CommunityFounderAvatar;

                // Saved user
                SavedUserId = view.SavedUserId;
                SavedUserUsername = view.SavedUserUsername;
                SavedUserAvatar = view.SavedUserAvatar;
                SavedUserBio = view.SavedUserBio;

                // Community discussion
                CommunityDiscussionId = view.CommunityDiscussionId;
                CommunityDiscussionTitle = view.CommunityDiscussionTitle;
                CommunityDiscussionContent = view.CommunityDiscussionContent;
                CommunityDiscussionCreatedAt = view.CommunityDiscussionCreatedAt;
                CommunityDiscussionUsername = view.CommunityDiscussionUsername;
                CommunityDiscussionAvatar = view.CommunityDiscussionAvatar;

                // Community discussion message
                CommunityDiscussionMessageId = view.CommunityDiscussionMessageId;
                CommunityDiscussionMessageContent = view.CommunityDiscussionMessageContent;
                CommunityDiscussionMessageMedia = view.CommunityDiscussionMessageMedia;
                CommunityDiscussionMessageCreatedAt = view.CommunityDiscussionMessageCreatedAt;
                CommunityDiscussionMessageUsername = view.CommunityDiscussionMessageUsername;
                CommunityDiscussionMessageAvatar = view.CommunityDiscussionMessageAvatar;
            }
        }

    }
}
