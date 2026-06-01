using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityDiscussionMessageRepository
    {
        Task<PaginatedResult<CommunityDiscussionMessageDto>> GetCommunityDiscussionMessages(
          Supabase.Client supabase,
          Guid communityDiscussionId,
          string? searchTerm,
          int currentPage,
          int itemsPerPage);
    }
}
