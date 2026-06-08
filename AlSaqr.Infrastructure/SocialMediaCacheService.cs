using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.Zook;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.SocialMedia.Messages;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    public interface ISocialMediaCacheService
    {
        void ClearUsersToAdd(Guid userId);
        void SetUsersToAdd(PaginatedResult<UserToAdd> pagination, Guid userId);
        bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, Guid userId);
        PaginatedResult<UserToAdd>? GetInitialUsersToAdd(Guid userId);

        void ClearInitialMessageThreads(Guid userId);
        void SetInitialMessageThreads(PaginatedResult<MessageHistoryDto> pagination, Guid userId);
        PaginatedResult<MessageHistoryDto>? GetInitialMessageThreads(Guid userId);
        bool CheckIfInitialMessageThreadsCanBeRetrieved(Guid userId);

        void ClearInitialCommunities(Guid userId);
        void SetInitialCommunities(PaginatedResult<CommunityDto> pagination, Guid userId);
        PaginatedResult<CommunityDto>? GetInitialCommunities(Guid userId);
        bool CheckIfInitialCommunitiesCanBeRetrieved(Guid userId);

        void ClearInitialCommunityDiscussions(Guid userId, Guid communityId);
        void SetInitialCommunityDiscussions(
            PaginatedResult<CommunityDiscussionDto> communityDiscussionsPagination,
            Guid userId,
            Guid communityId);
        PaginatedResult<CommunityDiscussionDto>? GetInitialCommunityDiscussions(Guid userId, Guid communityId);
        bool CheckIfInitialCommunityDiscussionsCanBeRetrieved(Guid userId, Guid communityId);

        void ClearInitialCommunityDiscussionMessages(Guid userId, Guid communityId, Guid communityDiscussionId);
        void SetInitialCommunityDiscussionMessages(
            PaginatedResult<CommunityDiscussionMessageDto> communityDiscussionMessagesPagination,
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId);
        PaginatedResult<CommunityDiscussionMessageDto>? GetInitialCommunityDiscussionMessages(Guid userId, Guid communityId, Guid communityDiscussionId);
        bool CheckIfInitialCommunityDiscussionMessagesCanBeRetrieved(Guid userId, Guid communityId, Guid communityDiscussionId);

        void ClearInitialLists(Guid userId);
        void SetInitialLists(PaginatedResult<ListDto> pagination, Guid userId);
        PaginatedResult<ListDto>? GetInitialLists(Guid userId);
        bool CheckIfInitialListsCanBeRetrieved(int currentPage, Guid userId);
        void ClearInitialListItemsForList(Guid userId, Guid listId, int currentPage);
        void SetInitialListItemForList(
            PaginatedResult<ListItemDto> userListsPagination,
            Guid userId,
            Guid listId,
            int currentPage);
        PaginatedResult<ListItemDto>? GetInitialListItemsForList(Guid userId, Guid listId, int currentPage);
        bool CheckIfInitialListItemForListCanBeRetrieved(int currentPage, Guid userId, Guid listId);

        void ClearInitialPosts();
        void SetInitialPosts(PaginatedResult<Dictionary<string, object>> pagination);
        PaginatedResult<Dictionary<string, object>>? GetInitialPosts();
        bool CheckIfInitialPostsCanBeRetrieved(int currentPage);
    }

    public partial class SocialMediaCacheService : ISocialMediaCacheService
    {

        private readonly IMemoryCache _cache;

        // Private constructor ensures singleton pattern
        public SocialMediaCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static MemoryCacheEntryOptions CommonCacheOptions => new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        };

        private static MemoryCacheEntryOptions ListsCacheOptions => new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        };

        private static MemoryCacheEntryOptions ListItemsCacheOptions => new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };

        private static MemoryCacheEntryOptions MessagesCacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(45),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        };
    }
}
