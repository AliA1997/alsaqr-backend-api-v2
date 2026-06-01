using AlSaqr.Domain.SocialMedia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IPostRepository
    {
        Task<PaginatedResult<PostDto>> GetBookmarkedPosts(
            Supabase.Client supabase,
            Guid userId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage,
            CancellationToken ct);

        Task<PaginatedResult<PostDto>> GetPosts(
            Supabase.Client supabase,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<PostDto> GetPost(
            Supabase.Client supabase,
            Guid postId);

        Task<Guid> CreatePost(
            Supabase.Client supabase,
            Guid userId,
            Posts.CreatePostDto data);

        Task<Guid> DeletePost(
           Supabase.Client supabase,
           Guid userId,
           Guid postId);
    }
}
