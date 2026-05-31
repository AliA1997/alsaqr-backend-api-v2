
using static AlSaqr.Domain.SocialMedia.Messages;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IMessageRepository
    {
        Task<PaginatedResult<MessageDto>> GetMessages(
             Supabase.Client supabase,
             Guid userId,
             string? searchTerm,
             int currentPage,
             int itemsPerPage);
    }
}
