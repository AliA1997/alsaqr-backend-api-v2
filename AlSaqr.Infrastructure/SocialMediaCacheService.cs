using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.Zook;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.SocialMedia.List;
using static AlSaqr.Domain.SocialMedia.Messages;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure.SocialMediaCache
{
    public interface ISocialMediaCacheService
    {
        /// USERS TO SHOW ON MODALS
        void ClearUsersToAdd(Guid userId);
        void SetUsersToAdd(PaginatedResult<UserToAdd> pagination, Guid userId);
        bool CheckIfInitialUsersToAddCanBeRetrieved(int currentPage, Guid userId);
        PaginatedResult<UserToAdd>? GetInitialUsersToAdd(Guid userId);
        //////////////////////////////////////////////////////////////////////////////
        
        /// MESSAGE THREADS FOR USER

        void ClearInitialMessageThreads(Guid userId);
        void SetInitialMessageThreads(PaginatedResult<MessageHistoryDto> pagination, Guid userId);
        PaginatedResult<MessageHistoryDto>? GetInitialMessageThreads(Guid userId);
        bool CheckIfInitialMessageThreadsCanBeRetrieved(Guid userId);
        //////////////////////////////////////////////////////////////////////////////

        /// COMMUNITIES FOR USER
        void ClearInitialCommunities(Guid userId);
        void SetInitialCommunities(PaginatedResult<CommunityDto> pagination, Guid userId);
        PaginatedResult<CommunityDto>? GetInitialCommunities(Guid userId);
        bool CheckIfInitialCommunitiesCanBeRetrieved(Guid userId);
        //////////////////////////////////////////////////////////////////////////////

        /// COMMUNITY DISCUSSIONS FOR USER
        void ClearInitialCommunityDiscussions(Guid userId, Guid communityId);
        void SetInitialCommunityDiscussions(
            PaginatedResult<CommunityDiscussionDto> communityDiscussionsPagination,
            Guid userId,
            Guid communityId
        );
        PaginatedResult<CommunityDiscussionDto>? GetInitialCommunityDiscussions(
            Guid userId,
            Guid communityId
        );
        bool CheckIfInitialCommunityDiscussionsCanBeRetrieved(Guid userId, Guid communityId);
        //////////////////////////////////////////////////////////////////////////////


        /// COMMUNITY DISCUSSION MESSAGES FOR USER
        void ClearInitialCommunityDiscussionMessages(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId
        );
        void SetInitialCommunityDiscussionMessages(
            PaginatedResult<CommunityDiscussionMessageDto> communityDiscussionMessagesPagination,
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId
        );
        PaginatedResult<CommunityDiscussionMessageDto>? GetInitialCommunityDiscussionMessages(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId
        );
        bool CheckIfInitialCommunityDiscussionMessagesCanBeRetrieved(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId
        );
        //////////////////////////////////////////////////////////////////////////////

        /// LISTS FOR USER
        void ClearInitialLists(Guid userId);
        void SetInitialLists(PaginatedResult<ListDto> pagination, Guid userId);
        PaginatedResult<ListDto>? GetInitialLists(Guid userId);
        bool CheckIfInitialListsCanBeRetrieved(int currentPage, Guid userId);
        //////////////////////////////////////////////////////////////////////////////

        /// LIST ITEMS FOR LIST PAGE
        void ClearInitialListItemsForList(Guid userId, Guid listId, int currentPage);
        void SetInitialListItemForList(
            PaginatedResult<ListItemDto> userListsPagination,
            Guid userId,
            Guid listId,
            int currentPage
        );
        PaginatedResult<ListItemDto>? GetInitialListItemsForList(
            Guid userId,
            Guid listId,
            int currentPage
        );
        bool CheckIfInitialListItemForListCanBeRetrieved(int currentPage, Guid userId, Guid listId);
        //////////////////////////////////////////////////////////////////////////////

        /// POSTS

        void ClearInitialPosts(Guid userId);
        void SetInitialPosts(Guid userId, PaginatedResult<PostDto> pagination);
        PaginatedResult<PostDto>? GetInitialPosts(Guid userId);
        bool CheckIfInitialPostsCanBeRetrieved(Guid userId);
        //////////////////////////////////////////////////////////////////////////////

        /// POSTS THAT DISPLAY IN MODALS
        void ClearInitialPostsToAdd(Guid userId);
        void SetInitialPostsToAdd(
            Guid userid,
            PaginatedResult<PostsToAdd> postsToAddPaginatedResult
        );
        bool CheckIfInitialPostsToAddCanBeRetrieved(Guid userid);
        PaginatedResult<PostsToAdd>? GetInitialPostsToAdd(Guid userid);
        //////////////////////////////////////////////////////////////////////////////
        
        /// COMMENTS
        void ClearInitialComments(Guid postId);
        void SetInitialComments(Guid postId, PaginatedResult<PostDto> pagination);
        PaginatedResult<PostDto>? GetInitialComments(Guid postId);
        bool CheckIfInitialCommentsCanBeRetrieved(Guid postId);
        //////////////////////////////////////////////////////////////////////////////

        /// EXPLORE ALL NEWS 
        void SetInitialExploreAllNews(
            PaginatedResult<Explore.ExploreToDisplay> exploreAllNewsPaginatedResult
        );
        bool CheckIfInitialExploreAllNewsCanBeRetrieved();
        PaginatedResult<Explore.ExploreToDisplay>? GetInitialAllExploreNews();
        //////////////////////////////////////////////////////////////////////////////

        /// EXPLORE NEWS BY SOURCE
        void SetInitialExploreNewsBySource(
            string source,
            PaginatedResult<Explore.ExploreToDisplay> exploreNewsBySourcePaginatedResult
        );
        bool CheckIfInitialExploreNewsBySourceCanBeRetrieved(string source);
        PaginatedResult<Explore.ExploreToDisplay>? GetInitialExploreNewsBySource(string source);
        //////////////////////////////////////////////////////////////////////////////

        /// PRODUCT CATEGORIES
        List<ProductCategoryDto>? GetInitialProductCategories();

        bool CheckIfInitialProductCategories();
        void SetInitialProductCategories(List<ProductCategoryDto> productCategoriesToSet);
        //////////////////////////////////////////////////////////////////////////////
    }

    public partial class SocialMediaCacheService : ISocialMediaCacheService
    {
        private readonly IMemoryCache _cache;
        const string productCategoriesKey = "productCategories";

        // Private constructor ensures singleton pattern
        public SocialMediaCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static MemoryCacheEntryOptions CommonCacheOptions =>
            new()
            {
                SlidingExpiration = TimeSpan.FromMinutes(5),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
            };

        private static MemoryCacheEntryOptions ExploreNewsCacheOptions =>
            new()
            {
                SlidingExpiration = TimeSpan.FromMinutes(15),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            };

        private static MemoryCacheEntryOptions ListsCacheOptions =>
            new()
            {
                SlidingExpiration = TimeSpan.FromMinutes(30),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
            };

        private static MemoryCacheEntryOptions ListItemsCacheOptions =>
            new()
            {
                SlidingExpiration = TimeSpan.FromMinutes(5),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            };

        private static MemoryCacheEntryOptions MessagesCacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(45),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
        };

        public void SetInitialProductCategories(List<ProductCategoryDto> productCategoriesToSet)
        {
            _cache.Set<List<ProductCategoryDto>>(productCategoriesKey, productCategoriesToSet);

            return;
        }

        public List<ProductCategoryDto>? GetInitialProductCategories()
        {
            _cache.TryGetValue(
                productCategoriesKey,
                out List<ProductCategoryDto>? productCategories
            );

            return productCategories;
        }

        public bool CheckIfInitialProductCategories()
        {
            _cache.TryGetValue(
                productCategoriesKey,
                out List<ProductCategoryDto>? productCategories
            );

            return (productCategories != null);
        }
    }
}
