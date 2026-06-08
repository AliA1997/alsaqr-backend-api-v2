using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string listsKeyPrefix = "initialLists_";
        private const string listItemsKeyPrefix = "initialListsItems_";

        // List Cache Functions
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

        // List Items Cache Functions
        public void ClearInitialListItemsForList(Guid userId, Guid listId, int currentPage)
        {
            _cache.Remove($"{listItemsKeyPrefix}{userId}_{listId}_{currentPage}");
        }
        public void SetInitialListItemForList(
            PaginatedResult<ListItemDto> userListsPagination, 
            Guid userId,
            Guid listId,
            int currentPage)
        {
            _cache.Set($"{listItemsKeyPrefix}{userId}_{listId}_{currentPage}", userListsPagination, ListItemsCacheOptions);
        }
        public PaginatedResult<ListItemDto>? GetInitialListItemsForList(Guid userId,  Guid listId, int currentPage)
        {
            _cache.TryGetValue($"{listItemsKeyPrefix}{userId}_{listId}_{currentPage}", out PaginatedResult<ListItemDto>? userListsPaginatedResult);

            return userListsPaginatedResult;
        }
        public bool CheckIfInitialListItemForListCanBeRetrieved(int currentPage, Guid userId, Guid listId)
        {
            _cache.TryGetValue($"{listItemsKeyPrefix}{userId}_{listId}_{currentPage}", out PaginatedResult<ListItemDto>? userListsPaginatedResult);

            return (userListsPaginatedResult != null && userListsPaginatedResult.Pagination.CurrentPage == currentPage);
        }
    }
}
