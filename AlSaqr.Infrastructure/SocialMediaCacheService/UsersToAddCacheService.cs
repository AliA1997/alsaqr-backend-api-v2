using AlSaqr.Domain.SocialMedia;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string usersToAddKeyPrefix = "initialUsersToAdd_";

        public void ClearUsersToAdd(Guid userId)
        {
            _cache.Remove($"{usersToAddKeyPrefix}{userId}");
        }
        public void SetUsersToAdd(PaginatedResult<UserToAdd> usersToAddPaginatedResult, Guid userId)
        {
            _cache.Set($"{usersToAddKeyPrefix}{userId}", usersToAddPaginatedResult, CommonCacheOptions);
        }
        public bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, Guid userId)
        {
            _cache.TryGetValue($"{usersToAddKeyPrefix}{userId}", out PaginatedResult<UserToAdd>? usersToAddPaginatedResult);

            return (usersToAddPaginatedResult != null && usersToAddPaginatedResult.Pagination.CurrentPage == currentPage);
        }
        public PaginatedResult<UserToAdd>? GetInitialUsersToAdd(Guid userId)
        {
            _cache.TryGetValue($"{usersToAddKeyPrefix}{userId}", out PaginatedResult<UserToAdd>? usersToAddPaginatedResult);

            return usersToAddPaginatedResult;
        }
    }
}
