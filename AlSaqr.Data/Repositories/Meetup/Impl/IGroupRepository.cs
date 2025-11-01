using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Groups;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IGroupRepository
    {
        Task<PaginatedResult<GroupDto>> GetNearbyGroups(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<PaginatedResult<GroupDto>> GetMyGroups(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string userId,
            string? searchTerm,
            double? maxDistanceKm);

        Task<List<SimilarGroupDto>> GetSimilarGroups(
            Supabase.Client client,
            int groupId,
            string latitude,
            string longitude);

        Task<Groups> CreateGroup(Supabase.Client client, CreateGroupForm form, string neo4jUserId, int organizerId, int cityId);
    }
}
