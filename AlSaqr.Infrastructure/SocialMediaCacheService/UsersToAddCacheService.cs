using AlSaqr.Domain.SocialMedia;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string usersToAddKeyPrefix = "initialUsersToAdd_";

        public void ClearUsersToAdd(string userId)
        {
            _cache.Remove($"{usersToAddKeyPrefix}{userId}");
        }
        public void SetUsersToAdd(PaginatedResult<UsersToAddDto> usersToAddPaginatedResult, string userId)
        {
            _cache.Set($"{usersToAddKeyPrefix}{userId}", usersToAddPaginatedResult, CommonCacheOptions);
        }
        public bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, string userId)
        {
            _cache.TryGetValue($"{usersToAddKeyPrefix}{userId}", out PaginatedResult<UsersToAddDto>? usersToAddPaginatedResult);

            return (usersToAddPaginatedResult != null && usersToAddPaginatedResult.Pagination.CurrentPage == currentPage);
        }
        public PaginatedResult<UsersToAddDto>? GetInitialUsersToAdd(string userId)
        {
            _cache.TryGetValue($"{usersToAddKeyPrefix}{userId}", out PaginatedResult<UsersToAddDto>? usersToAddPaginatedResult);

            return usersToAddPaginatedResult;
        }
    }
}
