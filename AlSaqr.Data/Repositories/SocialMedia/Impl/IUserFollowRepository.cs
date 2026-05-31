
using static AlSaqr.Domain.SocialMedia.User;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IUserFollowRepository
    {

        Task<(Guid, Guid)> AddUserFollow(Supabase.Client client, Guid userId, FollowUserFormDto  userFollowFormDto);

        Task<(Guid, Guid)> RemoveUserFollow(Supabase.Client client, Guid userId, UnFollowUserFormDto userFollowFormDto);

    }
}
