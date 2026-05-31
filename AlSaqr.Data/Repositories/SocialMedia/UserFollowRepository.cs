using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia.Exceptions;
using static AlSaqr.Domain.SocialMedia.User;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class UserFollowRepository: IUserFollowRepository
    {
        public UserFollowRepository() { }


        public async Task<(Guid, Guid)> AddUserFollow(Supabase.Client client, Guid userId, FollowUserFormDto userFollowFormDto)
        {
            try {
                await client.From<UserFollow>().Insert(new UserFollow
                {
                    FollowerId = userId,
                    FollowingId = userFollowFormDto.UserToFollowId,
                    CreatedAt = DateTime.UtcNow
                });


                return (userId, userFollowFormDto.UserToFollowId);
            }
            catch (AddUserFollowException ex)
            {
                    throw ex;
            }
            catch(Exception ex) 
            {
                throw new AddUserFollowException(
                    userId: userFollowFormDto.UserToFollowId,
                    followedUserId: userId,
                    innerException: ex
                );
            }
        }

        public async Task<(Guid, Guid)> RemoveUserFollow(Supabase.Client client, Guid userId, UnFollowUserFormDto userFollowFormDto)
        {
            try
            {
                await client.From<UserFollow>().Where(uf => uf.FollowerId == userId && uf.FollowingId == userFollowFormDto.UserToUnFollowId).Delete();

                return (userId, userFollowFormDto.UserToUnFollowId);
            }
            catch (RemoveUserFollowException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new RemoveUserFollowException(
                    userId: userId,
                    unfollowedUserId: userFollowFormDto.UserToUnFollowId,
                    innerException: ex
                );
            }
        }
    }
}
