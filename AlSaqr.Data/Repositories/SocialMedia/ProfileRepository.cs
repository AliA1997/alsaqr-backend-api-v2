using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using global::AlSaqr.Data.Entities.SocialMedia;
using global::AlSaqr.Data.Entities.SocialMedia.Views;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class ProfileRepository : IProfileRepository
    {
        // Relation-type tags carried over from the Neo4j UNION query.
        const string RELATION_USER = "user";
        const string RELATION_BOOKMARKED = "bookmarked";
        const string RELATION_LIKED = "liked";
        const string RELATION_REPOSTED = "reposted";
        const string RELATION_REPLIED = "replied";

        public ProfileRepository() { }

        /// <summary>
        /// Gets profile info for a username: user details, bookmark ids,
        /// following/followers and their counts. Sourced from vw_user_profile_info.
        /// </summary>
        public async Task<ProfileInfoDto> GetProfileInfo(
            Supabase.Client supabase,
            string username)
        {
            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var profileInfo = await supabase
                    .From<VwUserProfileInfo>()
                    .Where(x => x.Username == username)
                    .Single(ct);

                if (profileInfo == null)
                    throw new Exception($"User Profile with a username of {username} not found");

                return new ProfileInfoDto(profileInfo);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Gets a user's profile posts bucketed by relation type
        /// (user / bookmarked / liked / reposted / replied).
        /// Sourced from vw_user_profile_posts, which keys on viewer_user_id,
        /// so the username is resolved to a user id first.
        /// </summary>
        public async Task<UserProfilePostsDto> GetProfilePosts(
            Supabase.Client supabase,
            string username,
            int currentPage,
            int itemsPerPage)
        {
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                // The posts view is keyed by viewer_user_id, but the endpoint
                // supplies a username — resolve it to the user's id first.
                var user = await supabase
                    .From<AlSaqrUser>()
                    .Where(u => u.Username == username)
                    .Single(ct);

                if (user == null)
                    throw new Exception($"User Profile with a username of {username} not found");

                var pageResult = await supabase
                    .From<VwUserProfilePosts>()
                    .Where(x => x.ViewerUserId == user.Id)
                    .Order("post_created_at", Ordering.Descending)
                    .Range(skip, skip + itemsPerPage - 1)
                    .Get(ct);

                var posts = pageResult.Models
                    .Select(vwPost => new ProfilePostDto(vwPost))
                    .ToList();

                // Bucket by relation type, mirroring the five Neo4j UNION groups.
                return new UserProfilePostsDto
                {
                    UserPosts = posts.Where(p => p.PostRelationType == RELATION_USER).ToList(),
                    BookmarkedPosts = posts.Where(p => p.PostRelationType == RELATION_BOOKMARKED).ToList(),
                    LikedPosts = posts.Where(p => p.PostRelationType == RELATION_LIKED).ToList(),
                    RepostedPosts = posts.Where(p => p.PostRelationType == RELATION_REPOSTED).ToList(),
                    RepliedPosts = posts.Where(p => p.PostRelationType == RELATION_REPLIED).ToList(),
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
