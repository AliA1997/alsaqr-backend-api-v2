using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string communityKeyPrefix = "initialCommunities_";

        public void ClearInitialCommunities(string userId)
        {
            _cache.Remove($"{communityKeyPrefix}{userId}");
        }
        public void SetInitialCommunities(PaginatedResult<Dictionary<string, object>> userCommunitiesPagination, string userId)
        {
            _cache.Set($"{communityKeyPrefix}{userId}", userCommunitiesPagination, ListsCacheOptions);
        }
        public PaginatedResult<Dictionary<string, object>>? GetInitialCommunities(string userId)
        {
            _cache.TryGetValue($"{communityKeyPrefix}{userId}", out PaginatedResult<Dictionary<string, object>>? userCommunitiesPaginatedResult);

            return userCommunitiesPaginatedResult;
        }
        public bool CheckIfInitialCommunitiesCanBeRetrieved(int currentPage, string userId)
        {
            _cache.TryGetValue($"{communityKeyPrefix}{userId}", out PaginatedResult<Dictionary<string, object>>? userCommunitiesPaginatedResult);

            return (userCommunitiesPaginatedResult != null && userCommunitiesPaginatedResult.Pagination.CurrentPage == currentPage);
        }
    }
}
