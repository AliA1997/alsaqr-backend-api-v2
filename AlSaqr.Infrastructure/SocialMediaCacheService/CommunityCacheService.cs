using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string communityKeyPrefix = "initialCommunities_";

        public void ClearInitialCommunities(Guid userId)
        {
            _cache.Remove($"{communityKeyPrefix}{userId}");
        }
        public void SetInitialCommunities(PaginatedResult<CommunityDto> userCommunitiesPagination, Guid userId)
        {
            _cache.Set($"{communityKeyPrefix}{userId}", userCommunitiesPagination, ListsCacheOptions);
        }
        public PaginatedResult<CommunityDto>? GetInitialCommunities(Guid userId)
        {
            _cache.TryGetValue($"{communityKeyPrefix}{userId}", out PaginatedResult<CommunityDto>? userCommunitiesPaginatedResult);

            return userCommunitiesPaginatedResult;
        }
        public bool CheckIfInitialCommunitiesCanBeRetrieved(Guid userId)
        {
            _cache.TryGetValue($"{communityKeyPrefix}{userId}", out PaginatedResult<CommunityDto>? userCommunitiesPaginatedResult);

            return (userCommunitiesPaginatedResult != null);
        }

    }
}
