using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        private const string communityDiscussionsKeyPrefix = "initialCommunityDiscussions_";
        private const string communityDiscussionMessagesKeyPrefix = "initialCommunityDiscussionMessages_";


        public void ClearInitialCommunityDiscusssion(Guid userId, Guid communityId)
        {
            _cache.Remove($"{communityDiscussionsKeyPrefix}{userId}_{communityId}");
        }
        public void SetInitialCommunityDiscussions(
            PaginatedResult<CommunityDiscussionDto> communityDiscussionsPagination, 
            Guid userId,
            Guid communityId)
        {
            _cache.Set($"{communityDiscussionsKeyPrefix}{userId}_{communityId}", communityDiscussionsPagination, CommonCacheOptions);
        }
        public PaginatedResult<CommunityDiscussionDto>? GetInitialCommunityDiscussions(Guid userId, Guid communityId)
        {
            _cache.TryGetValue($"{communityDiscussionsKeyPrefix}{userId}_{communityId}", out PaginatedResult<CommunityDiscussionDto>? communityDiscussionsPaginatedResult);

            return communityDiscussionsPaginatedResult;
        }
        public bool CheckIfInitialCommunityDiscussionsCanBeRetrieved(Guid userId, Guid communityId)
        {
            _cache.TryGetValue($"{communityDiscussionsKeyPrefix}{userId}_{communityId}", out PaginatedResult<CommunityDiscussionDto>? communityDiscussionsPaginatedResult);

            return (communityDiscussionsPaginatedResult != null);
        }

        public void ClearInitialCommunityDiscusssionMessages(Guid userId, Guid communityId, Guid communityDiscussionId)
        {
            _cache.Remove($"{communityDiscussionMessagesKeyPrefix}{userId}_{communityId}_{communityDiscussionId}");
        }
        public void SetInitialCommunityDiscussionMessages(
            PaginatedResult<CommunityDiscussionMessageDto> communityDiscussionMessagesPagination,
            Guid userId,
            Guid communityId, 
            Guid communityDiscussionId)
        {
            _cache.Set($"{communityDiscussionMessagesKeyPrefix}{userId}_{communityId}_{communityDiscussionId}", communityDiscussionMessagesPagination, CommonCacheOptions);
        }
        public PaginatedResult<CommunityDiscussionMessageDto>? GetInitialCommunityDiscussionMessages(Guid userId, Guid communityId, Guid communityDiscussionId)
        {
            _cache.TryGetValue($"{communityDiscussionMessagesKeyPrefix}{userId}_{communityId}_{communityDiscussionId}", out PaginatedResult<CommunityDiscussionMessageDto>? communityDiscussionMessagesPaginatedResult);

            return communityDiscussionMessagesPaginatedResult;
        }
        public bool CheckIfInitialCommunityDiscussionMessagesCanBeRetrieved(Guid userId, Guid communityId, Guid communityDiscussionId)
        {
            _cache.TryGetValue($"{communityDiscussionMessagesKeyPrefix}{userId}_{communityId}_{communityDiscussionId}", out PaginatedResult<CommunityDiscussionMessageDto>? communityDiscussionMessagesPaginatedResult);

            return (communityDiscussionMessagesPaginatedResult != null);
        }
    }

}
