
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using Supabase.Gotrue;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class NotificationRepository : INotificationRepository
    {
        public NotificationRepository() { }

        public async Task<PaginatedResult<NotificationDto>> GetNotifications(
            Supabase.Client supabase,
            Guid userId,
            string? getAll,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var notifications = new List<NotificationDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {

                var parameters = new Dictionary<string, dynamic>
                {
                    { "p_user_id", userId }
                };

                if (!string.IsNullOrEmpty(getAll) && getAll.ToLower() == "true")
                {
                    parameters.Add("p_read", true);
                }
                else if (!string.IsNullOrEmpty(getAll) && getAll.ToLower() == "false")
                {
                    parameters.Add("p_read", false);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    parameters.Add("p_search_term", searchTerm);
                }

                var countResult = await SupabaseHelper.CallFunction(supabase, "get_notifications_details_count", parameters);
                var totalItems = countResult != null ? long.Parse(countResult) : 0;
                if (totalItems == 0)
                {
                    return new PaginatedResult<NotificationDto>(
                        notifications,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }

                var dataQuery = supabase
                                    .From<VwNotificationDetails>()
                                    .Where(x => x.UserId == userId);

                if (!string.IsNullOrEmpty(getAll) && getAll.ToLower() == "true")
                {
                    dataQuery = dataQuery.Where(x => x.IsRead == true);
                }
                else if (!string.IsNullOrEmpty(getAll) && getAll.ToLower() == "false")
                {
                    dataQuery = dataQuery.Where(x => x.IsRead == false);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    dataQuery = dataQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var pageResult = await dataQuery
                    .Order(x => x.NotificationCreatedAt, Ordering.Descending)
                    .Range(skip, skip + itemsPerPage - 1)
                    .Get();

                notifications = pageResult.Models.Select(vwNotification => new NotificationDto(vwNotification)).ToList();

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = (int)totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<NotificationDto>(notifications, pagination!);

        }

        public async Task<Guid> CreateNotification(
            Supabase.Client supabase, 
            Guid userId, 
            string notificationMsg, 
            string notificationType,
            string link,
            string entityType,
            Guid relatedEntityId)
        {
            var notification = new Notification()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = notificationMsg,
                NotificationType = notificationType,
                Link = link
            };

            switch(entityType)
            {
                case "post":
                    notification.PostId = relatedEntityId;
                    break;
                case "community":
                    notification.CommunityId = relatedEntityId;
                    break;
                case "community_discussion":
                    notification.CommunityDiscussionId = relatedEntityId;
                    break;
                case "community_discussion_message":
                    notification.CommunityDiscussionMessageId = relatedEntityId;
                    break;
                case "list":
                    notification.ListId = relatedEntityId;
                    break;
                case "user":
                    notification.RelatedUserId = relatedEntityId;
                    break;
                default:
                    throw new ArgumentException($"Unsupported entity type: {entityType}");
            }

 
            var newNotification = await supabase.From<Notification>().Insert(notification, new QueryOptions()
            {
                Returning = ReturnType.Representation

            });

            if (newNotification == null)
            {
                throw new Exception($"Error creating notification");
            }

            return newNotification.Model.Id;
        }
    }
}
