using Microsoft.Extensions.Caching.Memory;
using static AlSaqr.Domain.Utils.Session;

namespace AlSaqr.Infrastructure
{
    public interface IUserCacheService
    {
        void SetLoggedInUser(SessionUser user);
        SessionUser? GetLoggedInUser();
        void RemoveLoggedInUser(string userId);
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
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        };

        public void SetLoggedInUser(SessionUser user)
        {
            if (user == null)
                return;

            _cache.Set("loggedInUser", user, CacheOptions);
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
    }
}
