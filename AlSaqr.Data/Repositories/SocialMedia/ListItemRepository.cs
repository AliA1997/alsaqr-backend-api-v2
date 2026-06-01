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
    public class ListItemRepository: IListItemRepository
    {
        const string USER_ITEM_TYPE = "user";
        const string POST_ITEM_TYPE = "post";
        const string COMMUNITY_ITEM_TYPE = "community";
        const string COMMUNITY_DISCUSSION_ITEM_TYPE = "community_discussion";
        const string COMMUNITY_DISCUSSION_MESSAGE_ITEM_TYPE = "community_discussion_message";


        public ListItemRepository() { }


        public async Task<PaginatedResult<ListItemDto>> GetListItems(
            Supabase.Client supabase,
            Guid userId,
            Guid listId,
            int currentPage,
            int itemsPerPage)
        {
            var listItems = new List<ListItemDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;


            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var list = await supabase.From<Entities.SocialMedia.List>()
                                        .Where(x => x.Id == listId)
                                        .Single(ct);

                if (list == null || list.UserId != userId)
                    throw new Exception("List is not found");

                var baseQuery = supabase.From<VwListItemDetails>().Where(x => x.ListId == listId);
                var totalParams = new Dictionary<string, dynamic>()
                {
                    { "p_list_id",  listId }
                };

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_list_items_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<ListItemDto>(
                        listItems,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                listItems = (await baseQuery.Order("saved_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwListItem => new ListItemDto(vwListItem))
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

            return new PaginatedResult<ListItemDto>(listItems, pagination!);
        }


        public async Task<Guid> SaveItemToList(
            Supabase.Client supabase,
            Guid userId,
            Guid listId,
            SaveItemToListDto data,
            CancellationToken ct)
        {
            try
            {
                // Verify list ownership before mutating anything.
                var list = await supabase
                    .From<Entities.SocialMedia.List>()
                    .Where(l => l.Id == listId && l.UserId == userId)
                    .Single(ct);

                if (list == null)
                    throw new Exception("List not found");

                if (data.RelatedEntityId == Guid.Empty)
                    throw new ArgumentException("RelatedEntityId must be a valid GUID");

                switch (data.Type)
                {
                    case POST_ITEM_TYPE:
                        await AddPostsToList(supabase, listId, new List<Guid> { data.RelatedEntityId }, ct);
                        break;

                    case USER_ITEM_TYPE:
                        await AddUsersToList(supabase, listId, new List<Guid> { data.RelatedEntityId }, ct);
                        break;

                    case COMMUNITY_ITEM_TYPE:
                        await AddCommunityToList(supabase, listId, data.RelatedEntityId, ct);
                        break;

                    case COMMUNITY_DISCUSSION_ITEM_TYPE:
                        await AddCommunityDiscussionToList(supabase, listId, data.RelatedEntityId, ct);
                        break;

                    case COMMUNITY_DISCUSSION_MESSAGE_ITEM_TYPE:
                        await AddCommunityDiscussionMessageToList(supabase, listId, data.RelatedEntityId, ct);
                        break;

                    default:
                        throw new ArgumentException($"Unsupported list item type: {data.Type}");
                }

                return listId;
            }
            catch(SavedItemToListException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new SavedItemToListException(listId, data.RelatedEntityId, ex);
            }

        }


        public async Task<Guid> DeleteListItem(
            Supabase.Client supabase,
            Guid listId,
            Guid listItemId)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {

                ListItem? listItemToDelete = await supabase
                    .From<ListItem>()
                    .Where(c => c.Id == listItemId && c.ListId == listId)
                    .Single(ct);

                if (listItemToDelete == null)
                    throw new Exception("Can't delete the list item");

                var deleted = await supabase.From<ListItem>().Delete(listItemToDelete, new QueryOptions() {  Returning = ReturnType.Minimal }, ct);

                if (deleted == null || deleted.Model == null)
                    throw new Exception("Issue deleting list item");

                return deleted.Model.Id;
            } 
            catch(DeleteListItemException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new DeleteListItemException(listItemId, ex);
            }
        }


        public async Task AddUsersToList(
            Supabase.Client supabase,
            Guid listId,
            List<Guid> userIds,
            CancellationToken ct)
        {
            var savedListItems = userIds.Select(userId => new ListItem
            {
                Id = Guid.NewGuid(),
                ListId = listId,
                UserId = userId,
                ItemType = USER_ITEM_TYPE,
                SavedAt = DateTime.UtcNow
            }).ToList();

            var inserted = await supabase
                .From<ListItem>()
                .Insert(savedListItems, new QueryOptions
                {
                    Returning = ReturnType.Minimal
                });
            if (inserted == null)
                throw new Exception("Error adding items to a list.");


            await CreateListItemNotification(
                supabase,
                listId,
                "Users have been added to your list {list}.",
                "users_added_to_list",
                ct);

            return;
        }

        public async Task AddPostsToList(
            Supabase.Client supabase,
            Guid listId,
            List<Guid> postIds,
            CancellationToken ct)
        {
            var savedListItems = postIds.Select(postId => new ListItem
            {
                Id = Guid.NewGuid(),
                ListId = listId,
                PostId = postId,
                ItemType = POST_ITEM_TYPE,
                SavedAt = DateTime.UtcNow
            }).ToList();

            var inserted = await supabase
                .From<ListItem>()
                .Insert(savedListItems, new QueryOptions
                {
                    Returning = ReturnType.Minimal
                });
            if (inserted == null)
                throw new Exception("Error adding items to a list.");

            await CreateListItemNotification(
                supabase,
                listId,
                "Posts have been added to your list {list}.",
                "posts_added_to_list",
                ct);

            return;
        }

        private async Task AddCommunityToList(
            Supabase.Client supabase,
            Guid listId,
            Guid communityId,
            CancellationToken ct)
        {
            // Duplicate guard: skip if this community is already in the list.
            var existing = await supabase
                .From<ListItem>()
                .Where(li => li.ListId == listId && li.CommunityId == communityId)
                .Filter("item_type", Operator.Equals, COMMUNITY_ITEM_TYPE)
                .Single(ct);

            if (existing != null)
                return;

            var listItem = new ListItem
            {
                Id = Guid.NewGuid(),
                ListId = listId,
                CommunityId = communityId,
                ItemType = COMMUNITY_ITEM_TYPE,
                SavedAt = DateTime.UtcNow
            };

            var inserted = await supabase
                .From<ListItem>()
                .Insert(listItem, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (inserted == null)
                throw new Exception("Error adding community to list.");

            await CreateListItemNotification(
                supabase,
                listId,
                "A community has been added to your list {list}.",
                "community_added_to_list",
                ct);
        }

        private async Task AddCommunityDiscussionToList(
            Supabase.Client supabase,
            Guid listId,
            Guid communityDiscussionId,
            CancellationToken ct)
        {
            // Duplicate guard: skip if this discussion is already in the list.
            var existing = await supabase
                .From<ListItem>()
                .Where(li => li.ListId == listId && li.CommunityDiscussionId == communityDiscussionId)
                .Filter("item_type", Operator.Equals, COMMUNITY_DISCUSSION_ITEM_TYPE)
                .Single(ct);

            if (existing != null)
                return;

            var listItem = new ListItem
            {
                Id = Guid.NewGuid(),
                ListId = listId,
                CommunityDiscussionId = communityDiscussionId,
                ItemType = COMMUNITY_DISCUSSION_ITEM_TYPE,
                SavedAt = DateTime.UtcNow
            };

            var inserted = await supabase
                .From<ListItem>()
                .Insert(listItem, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (inserted == null)
                throw new Exception("Error adding community discussion to list.");

            await CreateListItemNotification(
                supabase,
                listId,
                "A community discussion has been added to your list {list}.",
                "community_discussion_added_to_list",
                ct);
        }

        private async Task AddCommunityDiscussionMessageToList(
            Supabase.Client supabase,
            Guid listId,
            Guid communityDiscussionMessageId,
            CancellationToken ct)
        {
            // Duplicate guard: skip if this message is already in the list.
            var existing = await supabase
                .From<ListItem>()
                .Where(li => li.ListId == listId && li.CommunityDiscussionMessageId == communityDiscussionMessageId)
                .Filter("item_type", Operator.Equals, COMMUNITY_DISCUSSION_MESSAGE_ITEM_TYPE)
                .Single(ct);

            if (existing != null)
                return;

            var listItem = new ListItem
            {
                Id = Guid.NewGuid(),
                ListId = listId,
                CommunityDiscussionMessageId = communityDiscussionMessageId,
                ItemType = COMMUNITY_DISCUSSION_MESSAGE_ITEM_TYPE,
                SavedAt = DateTime.UtcNow
            };

            var inserted = await supabase
                .From<ListItem>()
                .Insert(listItem, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (inserted == null)
                throw new Exception("Error adding community discussion message to list.");

            await CreateListItemNotification(
                supabase,
                listId,
                "A community discussion message has been added to your list {list}.",
                "community_discussion_message_added_to_list",
                ct);
        }


        public async Task CreateListItemNotification(
            Supabase.Client supabase,
            Guid listId,
            string messageTemplate,
            string notificationType,
            CancellationToken ct)
        {

            var list = await supabase
                .From<Entities.SocialMedia.List>()
                .Where(c => c.Id == listId)
                .Single(ct);

            var message = messageTemplate
                .Replace("{list}", list.Name);

            if (list == null)
                return;
            Notification? notification;

            notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = list.UserId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "user",
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
