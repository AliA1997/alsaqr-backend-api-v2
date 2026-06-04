using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.SocialMedia.User;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IUserRepository
    {
        Task<(Guid userId, string username)> GetUserIdAndUsernameByEmail(Supabase.Client supabase, string email);
        Task<AlSaqrUser> CreateInitialUser(Supabase.Client supabase, CreateInitialUserDto newUser);
        Task<Guid> UpdateUser(Supabase.Client supabase, Guid userId, UpdateUserDto updatedUser, CancellationToken ct);

        Task<Guid> CompleteRegistration(
            Supabase.Client supabase,
            Guid userId,
            UserRegisterFormDto data,
            CancellationToken ct);

        Task<Guid> DeleteUser(Supabase.Client supabase, Guid userId);

        Task<PaginatedResult<UserToAdd>> GetUsersToAdd(
            Supabase.Client supabase,
            Guid userGuid,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<PaginatedResult<PostsToAdd>> GetPostsToAdd(
            Supabase.Client supabase,
            Guid userGuid,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);
    }
}
