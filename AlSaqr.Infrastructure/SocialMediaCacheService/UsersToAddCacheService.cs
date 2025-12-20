using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public void SetUsersToAdd(PaginatedResult<Dictionary<string, object>> usersToAddPaginatedResult, string userId)
        {
            _cache.Set($"{usersToAddKeyPrefix}{userId}", usersToAddPaginatedResult, CommonCacheOptions);
        }
        public bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, string userId)
        {
            _cache.TryGetValue($"{usersToAddKeyPrefix}{userId}", out PaginatedResult<Dictionary<string, object>>? usersToAddPaginatedResult);

            return (usersToAddPaginatedResult != null && usersToAddPaginatedResult.Pagination.CurrentPage == currentPage);
        }
        public PaginatedResult<Dictionary<string, object>>? GetInitialUsersToAdd(string userId)
        {
            _cache.TryGetValue($"{usersToAddKeyPrefix}{userId}", out PaginatedResult<Dictionary<string, object>>? usersToAddPaginatedResult);

            return usersToAddPaginatedResult;
        }
    }
}
