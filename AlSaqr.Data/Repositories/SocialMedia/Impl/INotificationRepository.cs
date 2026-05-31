
using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface INotificationRepository
    {
        Task<PaginatedResult<NotificationDto>> GetNotifications(
            Supabase.Client supabase,
            Guid userId,
            string? getAll,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<Guid> CreateNotification(
            Supabase.Client supabase,
            Guid userId,
            string notificationMsg,
            string notificationType,
            string link,
            string entityType,
            Guid relatedEntityId);
    }
}
