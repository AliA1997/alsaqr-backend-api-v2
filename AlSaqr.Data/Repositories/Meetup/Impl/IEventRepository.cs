using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Events;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IEventRepository
    {
        Task<PaginatedResult<EventDto>> GetNearbyEvents(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<PaginatedResult<EventDto>> GetNearbyOnlineEvents(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<PaginatedResult<EventDto>> GetMyEvents(
            Supabase.Client client,
            string userId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<Event> CreateEvent(
            Supabase.Client client,
            CreateEventForm form);
    }
}
