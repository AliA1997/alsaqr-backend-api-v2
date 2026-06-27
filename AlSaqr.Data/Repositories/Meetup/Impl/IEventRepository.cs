using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;
using static AlSaqr.Domain.Utils.Common;

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

        Task<EventDto> GetEventDetails(
            Supabase.Client client, 
            Guid eventId
        );

        Task<PaginatedResult<AttendedEventDto>> GetAttendedEvents(
            Supabase.Client client,
            string username,
            int currentPage,
            int itemsPerPage,
            string? searchTerm);

        Task<Event> CreateEvent(
            Guid userId,
            Supabase.Client client,
            CreateEventForm form,
            CancellationToken ct);

        Task<Event> UpdateEvent(
            Supabase.Client client,
            Guid eventId,
            Guid userId,
            UpsertEventForm form,
            CancellationToken ct);

        Task<Guid> DeleteEvent(
            Supabase.Client client,
            Guid eventId,
            Guid userId,
            CancellationToken ct);
    }
}
