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
            int localGuideId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);
    }
}
