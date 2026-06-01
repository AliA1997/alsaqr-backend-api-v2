using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityDiscussionRepository
    {
        Task<PaginatedResult<CommunityDiscussionDto>> GetCommunityDiscussions(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<CommunityDiscussionDto> GetCommunityDiscussion(
            Supabase.Client supabase,
            Guid communityDiscussionId);

        Task<Guid> CreateCommunityDiscussion(
                    Supabase.Client supabase,
                    Guid userId,
                    Guid communityId,
                    CreateCommunityDiscussionForm data);

        Task<AdminCommunityDiscussionInfoDto> GetAdminCommunityDiscussionInfo(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId);
    }
}
