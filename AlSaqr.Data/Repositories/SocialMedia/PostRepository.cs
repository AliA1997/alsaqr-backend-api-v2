using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class PostRepository: IPostRepository
    {
        public PostRepository() { }

        public async Task<PaginatedResult<PostDto>> GetBookmarkedPosts(
            Supabase.Client supabase,
            Guid userId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var posts = new List<PostDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {
                // 1. Get IDs of posts this user has bookmarked
                var bookmarks = await supabase
                    .From<PostStatus>()
                    .Where(x => x.UserId == userId)
                    .Filter("action", Operator.Equals, "bookmarked")
                    .Get();

                var postIds = bookmarks.Models
                    .Select(x => x.PostId.ToString())
                    .ToList();


                var bookmarkPosts = await supabase
                    .From<Post>()
                    .Where(x => x.UserId == userId)
                    .Get();

                if (!postIds.Any())
                {
                    return new PaginatedResult<PostDto>(
                        posts,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }

                var parameters = new Dictionary<string, object>
                {
                    { "p_post_ids", postIds },
                };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    parameters.Add("p_search_term", searchTerm);
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_post_details_count", parameters);
                //var result = await supabase.Rpc("get_post_details_count", parameters);

                var totalItems = result != null ? long.Parse(result) : 0;

                // 3. Fetch the current page
                var dataQuery = supabase
                    .From<VwPostDetails>()
                    .Filter("post_id", Operator.In, postIds);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    dataQuery = dataQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var pageResult = await dataQuery
                    .Order("post_created_at", Ordering.Descending)
                    .Range(skip, skip + itemsPerPage - 1)
                    .Get();

                posts = pageResult.Models.Select(vwPost => new PostDto(vwPost)).ToList();

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

            return new PaginatedResult<PostDto>(posts, pagination!);

        }
    
        public async Task<PaginatedResult<PostDto>> GetPosts(
            Supabase.Client supabase,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var posts = new List<PostDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            
            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                
                var baseQuery = supabase.From<VwPostDetails>().Where(x => x.PostId != null);
                var totalParams = new Dictionary<string, dynamic>();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    totalParams.Add("p_search_term", searchTerm);
                    baseQuery = baseQuery.Where(x => x.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                        //baseQuery = baseQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_posts_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<PostDto>(
                        posts,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                posts = (await baseQuery.Order("post_created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwPost => new PostDto(vwPost))
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

            return new PaginatedResult<PostDto>(posts, pagination!);
        }

        public async Task<PostDto> GetPost(
            Supabase.Client supabase,
            Guid postId)
        {
            try
            {
                var post = await supabase.From<VwPostDetails>().Where(x => x.PostId == postId).Single();

                if (post == null)
                    throw new Exception("Post not found.");

                return new PostDto(post);

            } catch(Exception ex)
            {
                throw ex;
            }
        }

        // -------------------------------------------------------------------------
        // CREATE
        // -------------------------------------------------------------------------

        /// <summary>
        /// Creates a new post row and associates it with the given user.
        /// Migrated from Neo4j CREATE (u)-[:POSTED]->(t:Post {...}).
        /// </summary>
        public async Task<Guid> CreatePost(
            Supabase.Client supabase,
            Guid userId,
            Posts.CreatePostDto data)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Content = data.Text,
                BannerImage = data.Image ?? string.Empty,
                Tags = data.Tags ?? Array.Empty<string>(),
                RelatedPostId = null,
                PostType = "post",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var inserted = await supabase
                .From<Post>()
                .Insert(post, new Supabase.Postgrest.QueryOptions
                {
                    Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation
                });

            if (inserted?.Model == null)
                throw new Exception("Error creating post");

            return inserted.Model.Id;
        }

        public async Task<Guid> DeletePost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId)
        {

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {
                var post = await supabase.From<Post>().Where(x => x.Id == postId && x.UserId == userId).Single(ct);
                if (post == null)
                    throw new DeletePostException(postId, new Exception("Post not found or user not authorized to delete."));

                var postResult = await supabase.From<Post>().Delete(post, new QueryOptions() {
                    Returning = QueryOptions.ReturnType.Representation
                }, ct);

                if (postResult.Model == null || postResult.Model.Id == Guid.Empty)
                    throw new DeletePostException(postId, new Exception("Error deleting post."));

                await DeletePostStatusForPost(supabase, postId, ct);

                return postResult.Model.Id;
            }
            catch (DeletePostException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new DeletePostException(postId, ex);
            }
        }

        private async Task DeletePostStatusForPost(
            Supabase.Client supabase,
            Guid postId,
            CancellationToken ct)
        {
            try
            {
                await supabase.From<PostStatus>().Where(x => x.PostId == postId).Delete(new QueryOptions()
                {
                    Returning = QueryOptions.ReturnType.Minimal
                }, ct);

                return;
            }
            catch (DeletePostStatusException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new DeletePostStatusException(postId);
            }
        }
    }
}
