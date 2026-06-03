using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.Zook;
using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Infrastructure
{
    public interface IUserCacheService
    {
        void SetLoggedInUser(SessionUser user);
        SessionUser? GetLoggedInUser();
        void SetProductCategory(Guid productCategoryId, ProductCategoryDto productCategory);
        ProductCategoryDto? GetProductCategory(Guid productCategoryId);
        void RemoveLoggedInUser(string userId);

        void SetUserBookmarks(Guid userId, PaginatedResult<PostDto> bookmarks);

        PaginatedResult<PostDto>? GetUserBookmarks(Guid userId);
    }

    public sealed class UserCacheService : IUserCacheService
    {
        private static readonly Lazy<UserCacheService> _instance =
            new(() => new UserCacheService());

        private readonly IMemoryCache _cache;

        // Singleton instance accessor
        public static UserCacheService Instance => _instance.Value;

        // Private constructor ensures singleton pattern
        private UserCacheService()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        private static MemoryCacheEntryOptions CacheOptions => new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(30),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)
        };

        public void SetProductCategory(Guid productCategoryId, ProductCategoryDto productCategory)
        {
            if (productCategory == null)
                return;

            _cache.Set($"category-{productCategoryId.ToString()}", productCategory, CacheOptions);
        }

        public void SetLoggedInUser(SessionUser user)
        {
            if (user == null)
                return;

            _cache.Set("loggedInUser", user, CacheOptions);
        }

        public ProductCategoryDto? GetProductCategory(Guid productCategoryId)
        {
            _cache.TryGetValue($"category-{productCategoryId.ToString()}", out ProductCategoryDto? productCategory);
            return productCategory;
        }

        public SessionUser? GetLoggedInUser()
        {
            _cache.TryGetValue("loggedInUser", out SessionUser? user);
            return user;
        }

        public void RemoveLoggedInUser(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                _cache.Remove(userId);
        }
        
        public void SetUserBookmarks(Guid userId, PaginatedResult<PostDto> bookmarks)
        {
            if (bookmarks == null)
                return;
            _cache.Set($"bookmarks-{userId.ToString()}", bookmarks, CacheOptions);
        }

        public PaginatedResult<PostDto>? GetUserBookmarks(Guid userId)
        {
            _cache.TryGetValue($"bookmarks-{userId.ToString()}", out PaginatedResult<PostDto>? bookmarks);
            return bookmarks;
        }
    }
}
