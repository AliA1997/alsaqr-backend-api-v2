using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class ListRepository: IListRepository
    {
        public ListRepository() { }

        public async Task<PaginatedResult<ListDto>> GetLists(
            Supabase.Client supabase,
            Guid userId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var lists = new List<ListDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;


            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var baseQuery = supabase.From<VwListDetails>().Where(x => x.UserId == userId);
                var totalParams = new Dictionary<string, dynamic>()
                {
                    { "p_user_id", userId }
                };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    totalParams.Add("p_search_term", searchTerm);
                    baseQuery = baseQuery.Where(x => x.ListName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_lists_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<ListDto>(
                        lists,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                lists = (await baseQuery.Order("list_created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwList => new ListDto(vwList))
                                .ToList();

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

            return new PaginatedResult<ListDto>(lists, pagination!);
        }


        public async Task<ListDto> GetList(Supabase.Client supabase, Guid listId)
        {
            VwListDetails? list = null;
            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                list = await supabase.From<VwListDetails>().Where(l => l.ListId == listId).Single();

                if (list == null)
                    throw new Exception("List doesn't exist.");

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new ListDto(list);
        }

        public async Task<Guid> CreateList(
              Supabase.Client supabase,
              Guid userId,
              CreateListFormDto data,
              CancellationToken ct)
        {
            try
            {
                var list = new Entities.SocialMedia.List()
                {
                    UserId = userId,
                    Name = data.Name,
                    BannerImage = data.AvatarOrBannerImage,
                    Tags = data.Tags ?? Array.Empty<string>(),
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await supabase
                    .From<Entities.SocialMedia.List>()
                    .Insert(list, new QueryOptions
                    {
                        Returning = ReturnType.Representation
                    }, ct);

                if (inserted?.Model == null)
                    throw new Exception("Error creating list");


                await CreateListNotification(
                    supabase,
                    userId,
                    inserted.Model.Id,
                    "You created the list {list}",
                    "list_created",
                    ct);

                return inserted.Model.Id;
            }
            catch(CreateListException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new CreateListException(userId, data.Name, ex);
            }

          
        }


        public async Task<Guid> DeleteList(
            Supabase.Client supabase,
            Guid userId,
            Guid listId)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {
                Entities.SocialMedia.List? listToDelete = await supabase
                                                            .From<Entities.SocialMedia.List>()
                                                            .Where(c => c.Id == listId && c.UserId == userId)
                                                            .Single(ct);

                if (listToDelete == null)
                    throw new Exception("Can't delete the list");

                var deleted = await supabase.From<Entities.SocialMedia.List>().Delete(listToDelete, new QueryOptions() { Returning = ReturnType.Representation }, ct);

                if (deleted == null || deleted.Model == null)
                    throw new Exception("Issue deleting list");


                await DeleteListItemsToList(supabase, listId, ct);

                await CreateListNotification(
                    supabase,
                    userId,
                    listId,
                    "You deleted the list {list}",
                    "list_deleted",
                    ct);

                return deleted.Model.Id;
            } 
            catch(DeleteListException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new DeleteListException(listId, ex);
            }


        }

        private async Task DeleteListItemsToList(
            Supabase.Client supabase,
            Guid listId,
            CancellationToken ct)
        {
            Guid currentListItemIdToRemove = Guid.Empty; 
            try
            {
                var listItemsToRemoveResponse = await supabase
                                            .From<ListItem>()
                                            .Where(li => li.ListId == listId)
                                            .Get(ct);

                var listItemsToRemove = listItemsToRemoveResponse.Models.Select(l => l.Id);

                foreach(var listItmId in listItemsToRemove)
                {
                    currentListItemIdToRemove = listItmId;

                    await supabase
                        .From<ListItem>()
                        .Where(li => li.Id == listItmId)
                        .Delete(new QueryOptions() { Returning = ReturnType.Minimal }, ct);     
                }

            }
            catch(DeleteListItemException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new DeleteListItemException(currentListItemIdToRemove, ex);
            }
        }

        private async Task CreateListNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid listId,
            string messageTemplate,
            string notificationType,
            CancellationToken ct)
        {
            var list = await supabase
                .From<Entities.SocialMedia.List>()
                .Where(l => l.Id == listId)
                .Single(ct);

            if (list == null || list.UserId == userId)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{list}", list.Name);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = list.UserId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "list",
                ListId = listId,
                Link = $"/lists/{listId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
