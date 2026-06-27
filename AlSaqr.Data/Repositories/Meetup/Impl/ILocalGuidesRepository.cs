using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface ILocalGuidesRepository
    {
        Task<PaginatedResult<LocalGuideDto>> GetNearbyLocalGuides(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<PaginatedResult<LocalGuideDto>> GetNearbyLocalGuidesForCurrentLocalGuide(
            Supabase.Client client,
            Guid localGuideId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<LocalGuideDetailsDto> GetLocalGuideDetails(
            Supabase.Client client,
            Guid localGuideId);

        Task<LocalGuides> CreateLocalGuide(
            Supabase.Client client,
            Guid userId,
            CreateLocalGuideForm form,
            List<Guid> cityIds,
            CancellationToken ct);

        Task<LocalGuides> UpdateLocalGuide(
            Supabase.Client client,
            Guid localGuideId,
            Guid userId,
            UpsertLocalGuideForm form,
            List<Guid> cityIds,
            CancellationToken ct);

        Task<Guid> DeleteLocalGuide(
            Supabase.Client client,
            Guid localGuideId,
            Guid userId,
            CancellationToken ct);
    }
}
