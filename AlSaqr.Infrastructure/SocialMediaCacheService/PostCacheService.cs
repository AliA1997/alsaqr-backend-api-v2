using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        public void ClearInitialPosts()
        {
            _cache.Remove("initialPosts");
        }
        public void SetInitialPosts(PaginatedResult<Dictionary<string, object>> postsPaginatedResult)
        {
            _cache.Set("initialPosts", postsPaginatedResult, CommonCacheOptions);
        }
        public bool CheckIfInitialPostsCanBeRetrieved(int currentPage)
        {
            _cache.TryGetValue("initialPosts", out PaginatedResult<Dictionary<string, object>>? postsPaginatedResult);

            return (postsPaginatedResult != null && postsPaginatedResult.Pagination.CurrentPage == currentPage);
        }
        public PaginatedResult<Dictionary<string, object>>? GetInitialPosts()
        {
            _cache.TryGetValue("initialPosts", out PaginatedResult<Dictionary<string, object>>? postsPaginatedResult);

            return postsPaginatedResult;
        }
    }
}
