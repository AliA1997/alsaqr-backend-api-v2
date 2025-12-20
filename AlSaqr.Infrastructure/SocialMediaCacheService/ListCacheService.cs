using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string listsKeyPrefix = "initialLists_";
        public void ClearInitialLists(string userId)
        {
            _cache.Remove($"{listsKeyPrefix}{userId}");
        }
        public void SetInitialLists(PaginatedResult<Dictionary<string, object>> userListsPagination, string userId)
        {
            _cache.Set($"{listsKeyPrefix}{userId}", userListsPagination, ListsCacheOptions);
        }
        public PaginatedResult<Dictionary<string, object>>? GetInitialLists(string userId)
        {
            _cache.TryGetValue($"{listsKeyPrefix}{userId}", out PaginatedResult<Dictionary<string, object>>? userListsPaginatedResult);

            return userListsPaginatedResult;
        }
        public bool CheckIfInitialListsCanBeRetrieved(int currentPage, string userId)
        {
            _cache.TryGetValue($"{listsKeyPrefix}{userId}", out PaginatedResult<Dictionary<string, object>>? userListsPaginatedResult);

            return (userListsPaginatedResult != null && userListsPaginatedResult.Pagination.CurrentPage == currentPage);
        }
    }
}
