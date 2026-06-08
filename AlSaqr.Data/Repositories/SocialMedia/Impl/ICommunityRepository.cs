using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityRepository
    {
        Task<PaginatedResult<CommunityDto>> GetCommunities(
            Supabase.Client supabase,
            Guid userId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<CommunityDto> GetCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid commmunityId);

        Task<AdminCommunityDto> GetAdminCommunityInfo(
                    Supabase.Client supabase,
                    Guid userId,
                    Guid commmunityId);


        Task<Guid> CreateCommunity(
              Supabase.Client supabase,
              Guid userId,
              CreateCommunityFormDto data);

        Task<Guid> UpdateCommunity(
            Supabase.Client client,
            Guid userId,
            Guid communityId,
            UpdateCommunityForm updatedCommunity,
            CancellationToken ct);

        Task<Guid> DeleteCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId);
    }
}
