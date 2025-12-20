using AlSaqr.Domain.Zook;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Session;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    public interface ISocialMediaCacheService
    {
        void ClearUsersToAdd(string userId);
        void SetUsersToAdd(PaginatedResult<Dictionary<string, object>> pagination, string userId);
        bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, string userId);
        PaginatedResult<Dictionary<string, object>>? GetInitialUsersToAdd(string userId);

        void ClearInitialCommunities(string userId);
        void SetInitialCommunities(PaginatedResult<Dictionary<string, object>> pagination, string userId);
        PaginatedResult<Dictionary<string, object>>? GetInitialCommunities(string userId);
        bool CheckIfInitialCommunitiesCanBeRetrieved(int currentPage, string userId);

        void ClearInitialLists(string userId);
        void SetInitialLists(PaginatedResult<Dictionary<string, object>> pagination, string userId);
        PaginatedResult<Dictionary<string, object>>? GetInitialLists(string userId);
        bool CheckIfInitialListsCanBeRetrieved(int currentPage, string userId);

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
