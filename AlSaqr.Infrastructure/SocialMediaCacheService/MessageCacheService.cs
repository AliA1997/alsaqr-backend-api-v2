using AlSaqr.Domain.SocialMedia;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    public partial class SocialMediaCacheService
    {
        private const string messagesKeyPrefix = "initialMessages_";

        public void ClearInitialMessageThreads(Guid userId)
        {
            _cache.Remove($"{messagesKeyPrefix}{userId}");
        }
        public void SetInitialMessageThreads(PaginatedResult<Messages.MessageHistoryDto> pagination, Guid userId)
        {
            _cache.Set($"{messagesKeyPrefix}{userId}", pagination, MessagesCacheOptions);
        }
        public PaginatedResult<Messages.MessageHistoryDto>? GetInitialMessageThreads(Guid userId)
        {
            _cache.TryGetValue($"{messagesKeyPrefix}{userId}", out PaginatedResult<Messages.MessageHistoryDto>? messagesPaginatedResult);

            return messagesPaginatedResult;
        }
        public bool CheckIfInitialMessageThreadsCanBeRetrieved(Guid userId)
        {
            _cache.TryGetValue($"{messagesKeyPrefix}{userId}", out PaginatedResult<Messages.MessageHistoryDto>? messagesPaginatedResult);

            return (messagesPaginatedResult != null);
        }

    }
}
