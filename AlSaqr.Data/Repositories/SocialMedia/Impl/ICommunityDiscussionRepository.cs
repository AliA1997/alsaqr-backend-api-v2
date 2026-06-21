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
            Guid userId,
            Guid communityDiscussionId);

        Task<PaginatedResult<CommunityDiscussionDto>> GetUserCommunityDiscussions(
            Supabase.Client client,
            string username,
            int currentPage,
            int itemsPerPage,
            string? searchTerm);

        Task<Guid> CreateCommunityDiscussion(
                    Supabase.Client supabase,
                    Guid userId,
                    Guid communityId,
                    CreateCommunityDiscussionForm data);

        Task<Guid> UpdateCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            UpdateCommunityDiscussionForm data);
        Task<Guid> DeleteCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId);

        Task<AdminCommunityDiscussionInfoDto?> GetAdminCommunityDiscussionInfo(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId);

        Task<Guid> CreateCommunityDiscussionMessage(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            CreateCommunityDiscussionMessageForm data);
    }
}
