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


        public void ClearInitialPosts(Guid userId)
        {            
            _cache.Remove($"{postsPrefix}{userId}");
        }
        public void SetInitialPosts(Guid userId, PaginatedResult<PostDto> postsPaginatedResult)
        {

            _cache.Set($"{postsPrefix}{userId}", postsPaginatedResult, CommonCacheOptions);
        }
        public bool CheckIfInitialPostsCanBeRetrieved(Guid userId)
        {
            _cache.TryGetValue($"{postsPrefix}{userId}", out PaginatedResult<PostDto>? postsPaginatedResult);

            return (postsPaginatedResult != null);
        }
        public PaginatedResult<PostDto>? GetInitialPosts(Guid userId)
        {
            _cache.TryGetValue($"{postsPrefix}{userId}", out PaginatedResult<PostDto>? postsPaginatedResult);

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
