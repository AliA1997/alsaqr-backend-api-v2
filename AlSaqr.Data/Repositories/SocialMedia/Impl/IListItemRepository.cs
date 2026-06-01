using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IListItemRepository
    {

        Task<PaginatedResult<ListItemDto>> GetListItems(
            Supabase.Client supabase,
            Guid userId,
            Guid listId,
            int currentPage,
            int itemsPerPage);

        Task<Guid> SaveItemToList(
           Supabase.Client supabase,
           Guid userId,
           Guid listId,
           SaveItemToListDto data,
           CancellationToken ct);

        Task<Guid> DeleteListItem(
           Supabase.Client supabase,
           Guid listId,
           Guid listItemId);

        Task AddUsersToList(
            Supabase.Client supabase,
            Guid listId,
            List<Guid> userIds,
            CancellationToken ct);

        Task AddPostsToList(
            Supabase.Client supabase,
            Guid listId,
            List<Guid> postIds,
            CancellationToken ct);
    }
}
