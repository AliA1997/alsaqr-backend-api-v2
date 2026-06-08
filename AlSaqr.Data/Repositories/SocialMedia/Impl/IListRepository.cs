
using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IListRepository
    {
        Task<PaginatedResult<ListDto>> GetLists(
           Supabase.Client supabase,
           Guid userId,
           string? searchTerm,
           int currentPage,
           int itemsPerPage);

        Task<ListDto> GetList(
            Supabase.Client supabase,
            Guid listId);

        Task<Guid> CreateList(
              Supabase.Client supabase,
              Guid userId,
              CreateListFormDto data,
              CancellationToken ct);

        Task<Guid> DeleteList(
            Supabase.Client supabase,
            Guid userId,
            Guid listId);
    }
}
