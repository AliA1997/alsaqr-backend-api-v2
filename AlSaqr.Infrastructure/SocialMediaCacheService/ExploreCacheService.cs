
using AlSaqr.Domain.SocialMedia;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        const string exploreNewsKey = "exploreNews";
        const string exploreSourceNewsKeyPrefix = "exploreSourceNews_";

        public void SetInitialExploreAllNews(PaginatedResult<Explore.ExploreToDisplay> exploreAllNewsPaginatedResult)
        {

            _cache.Set($"{exploreNewsKey}", exploreAllNewsPaginatedResult, ExploreNewsCacheOptions);
        }
        public bool CheckIfInitialExploreAllNewsCanBeRetrieved()
        {
            _cache.TryGetValue($"{exploreNewsKey}", out PaginatedResult<Explore.ExploreToDisplay>? exploreAllNewsPaginatedResult);

            return (exploreAllNewsPaginatedResult != null);
        }
        public PaginatedResult<Explore.ExploreToDisplay>? GetInitialAllExploreNews()
        {
            _cache.TryGetValue($"{exploreNewsKey}", out PaginatedResult<Explore.ExploreToDisplay>? exploreAllNewsPaginatedResult);

            return exploreAllNewsPaginatedResult;
        }

        public void SetInitialExploreNewsBySource(string source, PaginatedResult<Explore.ExploreToDisplay> exploreNewsBySourcePaginatedResult)
        {

            _cache.Set($"{exploreSourceNewsKeyPrefix}{source}", exploreNewsBySourcePaginatedResult, ExploreNewsCacheOptions);
        }
        public bool CheckIfInitialExploreNewsBySourceCanBeRetrieved(string source)
        {
            _cache.TryGetValue($"{exploreSourceNewsKeyPrefix}{source}", out PaginatedResult<Explore.ExploreToDisplay>? exploreNewsBySourcePaginatedResult);

            return (exploreNewsBySourcePaginatedResult != null);
        }
        public PaginatedResult<Explore.ExploreToDisplay>? GetInitialExploreNewsBySource(string source)
        {
            _cache.TryGetValue($"{exploreSourceNewsKeyPrefix}{source}", out PaginatedResult<Explore.ExploreToDisplay>? exploreNewsBySourcePaginatedResult);

            return exploreNewsBySourcePaginatedResult;
        }
    }
}
