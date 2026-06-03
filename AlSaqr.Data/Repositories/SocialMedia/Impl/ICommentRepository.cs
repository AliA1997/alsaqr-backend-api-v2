using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommentRepository
    {
        Task<PaginatedResult<PostDto>> GetComments(
            Supabase.Client supabase,
            Guid postId,
            int currentPage,
            int itemsPerPage);

        Task<Guid> CreateComment(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            Posts.CreateCommentDto data,
            CancellationToken ct);
    }
}
