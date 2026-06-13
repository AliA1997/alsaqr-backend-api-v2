using AlSaqr.Domain.SocialMedia;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    partial class SocialMediaCacheService
    {
        const string postsToAddKeyPrefix = "initialPostsToAdd_";
        const string postsPrefix = "initialPosts_";
        const string commentsPrefix = "initialComments_";


        public void ClearInitialPosts(Guid userId, int currentPage)
        {
            // Means clear all initial posts cache for the user, as the change can affect all pages
            if (currentPage == -1)
                for(var pageKey = 1; pageKey <= 10; pageKey++)
                    _cache.Remove($"{postsPrefix}{userId}_page_{pageKey}");

        }
        public void SetInitialPosts(Guid userId, int currentPage, PaginatedResult<PostDto> postsPaginatedResult)
        {
            if (currentPage >= 10) return;

            _cache.Set($"{postsPrefix}{userId}_page_{currentPage}", postsPaginatedResult, CommonCacheOptions);
        }
        public bool CheckIfInitialPostsCanBeRetrieved(Guid userId, int currentPage)
        {
            _cache.TryGetValue($"{postsPrefix}{userId}_page_{currentPage}", out PaginatedResult<PostDto>? postsPaginatedResult);

            return (postsPaginatedResult != null && postsPaginatedResult.Pagination.CurrentPage == currentPage);
        }
        public PaginatedResult<PostDto>? GetInitialPosts(Guid userId, int currentPage)
        {
            _cache.TryGetValue($"{postsPrefix}{userId}_page_{currentPage}", out PaginatedResult<PostDto>? postsPaginatedResult);

            return postsPaginatedResult;
        }

        public void ClearInitialPostsToAdd(Guid userId)
        {
            _cache.Remove($"{postsToAddKeyPrefix}{userId}");
        }
        public void SetInitialPostsToAdd(Guid userid, PaginatedResult<PostsToAdd> postsToAddPaginatedResult)
        {
            _cache.Set($"{postsToAddKeyPrefix}{userid}", postsToAddPaginatedResult, ListsCacheOptions);
        }
        public bool CheckIfInitialPostsToAddCanBeRetrieved(Guid userid)
        {
            _cache.TryGetValue($"{postsToAddKeyPrefix}{userid}", out PaginatedResult<PostsToAdd>? postsToAddPaginatedResult);

            return postsToAddPaginatedResult != null;
        }
        public PaginatedResult<PostsToAdd>? GetInitialPostsToAdd(Guid userid)
        {
            _cache.TryGetValue($"{postsToAddKeyPrefix}{userid}", out PaginatedResult<PostsToAdd>? postsToAddPaginatedResult);

            return postsToAddPaginatedResult;
        }

        public void ClearInitialComments(Guid postId)
        {
            _cache.Remove($"{commentsPrefix}{postId}");
        }
        public void SetInitialComments(Guid postId, PaginatedResult<PostDto> pagination)
        {
            _cache.Set($"{commentsPrefix}{postId}", pagination, ListsCacheOptions);
        }
        public PaginatedResult<PostDto>? GetInitialComments(Guid postId)
        {
            _cache.TryGetValue($"{commentsPrefix}{postId}", out PaginatedResult<PostDto>? commentsPaginatedResult);

            return commentsPaginatedResult;
        }
        public bool CheckIfInitialCommentsCanBeRetrieved(Guid postId)
        {
            return _cache.TryGetValue($"{commentsPrefix}{postId}", out PaginatedResult<PostDto>? _);
        }
    }
}
