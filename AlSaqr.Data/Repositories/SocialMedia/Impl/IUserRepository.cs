using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.SocialMedia.User;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IUserRepository
    {
        Task<AlSaqrUser> CreateInitialUser(Supabase.Client supabase, CreateInitialUserDto newUser);
        Task<Guid> UpdateUser(Supabase.Client supabase, Guid userId, UpdateUserDto updatedUser, CancellationToken ct);

        Task<Guid> CompleteRegistration(
            Supabase.Client supabase,
            Guid userId,
            UserRegisterFormDto data);

        Task<Guid> DeleteUser(Supabase.Client supabase, Guid userId);

        Task<PaginatedResult<UsersToAddDto>> GetUsersToAdd(
            Supabase.Client supabase,
            Guid userGuid,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<PaginatedResult<PostsToAddDto>> GetPostsToAdd(
            Supabase.Client supabase,
            Guid userGuid,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);
    }
}
