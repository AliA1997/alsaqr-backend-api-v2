using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
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

                //var userProfiles = await supabase
                //                        .From<VwUserProfileInfo>()
                //                        .Where(x => x.UserId == userId)
                //                        .Get();

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
    }
}
