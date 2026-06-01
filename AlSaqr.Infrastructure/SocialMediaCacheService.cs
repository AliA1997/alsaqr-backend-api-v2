using AlSaqr.Domain.Zook;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.SocialMedia.Session;
using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.SocialMedia.List;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    public interface ISocialMediaCacheService
    {
        void ClearUsersToAdd(string userId);
        void SetUsersToAdd(PaginatedResult<UsersToAddDto> pagination, string userId);
        bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, string userId);
        PaginatedResult<UsersToAddDto>? GetInitialUsersToAdd(string userId);

        void ClearInitialCommunities(Guid userId);
        void SetInitialCommunities(PaginatedResult<CommunityDto> pagination, Guid userId);
        PaginatedResult<CommunityDto>? GetInitialCommunities(Guid userId);
        bool CheckIfInitialCommunitiesCanBeRetrieved(int currentPage, Guid userId);

        void ClearInitialLists(Guid userId);
        void SetInitialLists(PaginatedResult<ListDto> pagination, Guid userId);
        PaginatedResult<ListDto>? GetInitialLists(Guid userId);
        bool CheckIfInitialListsCanBeRetrieved(int currentPage, Guid userId);

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

    }
}
