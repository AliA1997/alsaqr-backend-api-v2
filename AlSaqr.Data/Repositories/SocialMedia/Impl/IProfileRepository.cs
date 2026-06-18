using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.SocialMedia.Session;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IProfileRepository
    {

        Task<SessionUser> GetSessionInfo(
            Supabase.Client supabase,
            Guid userId);

        Task<UserProfilePostsDto> GetProfilePosts(
            Supabase.Client supabase,
            string username,
            int currentPage,
            int itemsPerPage);
        Task<List<ProfilePostDto>> GetProfileMediaPosts(
            Supabase.Client supabase,
            string username,
            int currentPage,
            int itemsPerPage);

        Task<ProfileInfoDto> GetProfileInfo(
            Supabase.Client supabase,
            string username);
    }
}
