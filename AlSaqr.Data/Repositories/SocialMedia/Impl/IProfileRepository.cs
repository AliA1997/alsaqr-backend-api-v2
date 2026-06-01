using AlSaqr.Domain.SocialMedia;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IProfileRepository
    {
        Task<UserProfilePostsDto> GetProfilePosts(
            Supabase.Client supabase,
            string username,
            int currentPage,
            int itemsPerPage);

        Task<ProfileInfoDto> GetProfileInfo(
            Supabase.Client supabase,
            string username);
    }
}
