using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string listsKeyPrefix = "initialLists_";
        public void ClearInitialLists(Guid userId)
        {
            _cache.Remove($"{listsKeyPrefix}{userId}");
        }
        public void SetInitialLists(PaginatedResult<ListDto> userListsPagination, Guid userId)
        {
            _cache.Set($"{listsKeyPrefix}{userId}", userListsPagination, ListsCacheOptions);
        }
        public PaginatedResult<ListDto>? GetInitialLists(Guid userId)
        {
            _cache.TryGetValue($"{listsKeyPrefix}{userId}", out PaginatedResult<ListDto>? userListsPaginatedResult);

            return userListsPaginatedResult;
        }
        public bool CheckIfInitialListsCanBeRetrieved(int currentPage, Guid userId)
        {
            _cache.TryGetValue($"{listsKeyPrefix}{userId}", out PaginatedResult<ListDto>? userListsPaginatedResult);

            return (userListsPaginatedResult != null && userListsPaginatedResult.Pagination.CurrentPage == currentPage);
        }
    }
}
