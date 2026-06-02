using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
using AlSaqr.Domain.Utils;
using Newtonsoft.Json;
using Supabase.Interfaces;
using Supabase.Postgrest;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.SocialMedia.User;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class UserRepository : IUserRepository
    {
        public UserRepository() { }

        public async Task<PaginatedResult<UsersToAddDto>> GetUsersToAdd(
            Supabase.Client supabase, 
            Guid userGuid, 
            string? searchTerm, 
            int currentPage, 
            int itemsPerPage)
        {
            var usersToAdd = new List<UsersToAddDto>();
            Pagination? pagination = null;
            var functionName = "get_users_to_add";

            try
            {
                IDictionary<string, dynamic> functionParams = SupabaseHelper.DefineGetUsersToAddParams(
                    currentUserId: userGuid,
                    searchTerm: searchTerm,
                    currentPage: currentPage,
                    itemsPerPage: itemsPerPage
                );

                usersToAdd = JsonConvert.DeserializeObject<List<UsersToAddDto>>(
                    await SupabaseHelper.CallFunction(supabase, functionName, functionParams)
                ) ?? new List<UsersToAddDto>();

                int totalItems = usersToAdd.FirstOrDefault()?.TotalItems ?? 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<UsersToAddDto>(usersToAdd, pagination!);

        }

        public async Task<PaginatedResult<PostsToAddDto>> GetPostsToAdd(
            Supabase.Client supabase,
            Guid userGuid,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var postsToAdd = new List<PostsToAddDto>();
            Pagination? pagination = null;
            var functionName = "get_posts_to_add";

            try
            {
                IDictionary<string, dynamic> functionParams = SupabaseHelper.DefineGetPostsToAddParams(
                    currentUserId: userGuid,
                    searchTerm: searchTerm,
                    currentPage: currentPage,
                    itemsPerPage: itemsPerPage
                );

                postsToAdd = JsonConvert.DeserializeObject<List<PostsToAddDto>>(
                    await SupabaseHelper.CallFunction(supabase, functionName, functionParams)
                ) ?? new List<PostsToAddDto>();

                int totalItems = postsToAdd.FirstOrDefault()?.TotalItems ?? 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<PostsToAddDto>(postsToAdd, pagination!);
        }
        public async Task<AlSaqrUser> CreateInitialUser(Supabase.Client client, CreateInitialUserDto newUser)
        {

            var newUserEntity = new AlSaqrUser()
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Avatar = newUser.Avatar.ToString(),
                Email = newUser.Email,
                CreatedAt = newUser.CreatedAt,
                Hobbies = newUser.Hobbies,
                MaritalStatus = newUser.MaritalStatus,
                PreferredMadhab = newUser.PreferredMadhab,
                IslamicStudyTopics = newUser.IslamicStudyTopics,
                FavoriteIslamicScholars = newUser.FavoriteIslamicScholars,
                FavoriteQuranReciters = newUser.FavoriteQuranReciters,
                IsCompleted = false,
                IsVerified = false,
            };

            var insertedUser = (await client.From<AlSaqrUser>().Upsert(newUserEntity, new QueryOptions()
            {
                Returning = QueryOptions.ReturnType.Representation,
            })).Model;

            return insertedUser!;
        }

        public async Task<Guid> UpdateUser(
            Supabase.Client client, 
            Guid userId, 
            UpdateUserDto updatedUser,
            CancellationToken ct)
        {
            try
            {
                AlSaqrUser? userToUpdate = (await client.From<AlSaqrUser>().Where(u => u.Id == userId).Single());
                if (userToUpdate == null)
                    throw new Exception("User not found");

                userToUpdate.FirstName = Common.AssignStringValue(userToUpdate!.FirstName, updatedUser?.FirstName);
                userToUpdate.LastName = Common.AssignStringValue(userToUpdate!.LastName, updatedUser?.LastName);

                userToUpdate.Username = Common.AssignStringValue(userToUpdate.Username, updatedUser?.Username);
                userToUpdate.Avatar = Common.AssignStringValue(userToUpdate!.Avatar, updatedUser?.Avatar?.ToString());
                userToUpdate.Bio = Common.AssignStringValue(userToUpdate!.Bio, updatedUser?.Bio);
                userToUpdate.Hobbies = updatedUser?.Hobbies ?? new string[] { };
                userToUpdate.MaritalStatus = Common.AssignStringValue(userToUpdate.MaritalStatus, updatedUser?.MaritalStatus);
                userToUpdate.PreferredMadhab = Common.AssignStringValue(userToUpdate.PreferredMadhab, updatedUser?.PreferredMadhab);
                userToUpdate.IslamicStudyTopics = updatedUser?.IslamicStudyTopics ?? new string[] { };
                userToUpdate.FavoriteIslamicScholars = updatedUser?.FavoriteIslamicScholars ?? new string[] { }; ;
                userToUpdate.FavoriteQuranReciters = updatedUser?.FavoriteQuranReciters ?? new string[] { }; ;

                await client.From<AlSaqrUser>().Where(u => u.Id == userToUpdate!.Id).Upsert(userToUpdate);
                return userToUpdate.Id;
            }
            catch(UpdateUserException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new UpdateUserException(userId, ex);
            }

        }

        public async Task<Guid> CompleteRegistration(
            Supabase.Client supabase,
            Guid userId,
            UserRegisterFormDto data)
        {
            try
            {
                AlSaqrUser? userToUpdate = await supabase
                    .From<AlSaqrUser>()
                    .Where(u => u.Id == userId)
                    .Single();

                if (userToUpdate == null)
                    throw new Exception("User not found");

                userToUpdate.Username = Common.AssignStringValue(userToUpdate.Username, data.Username);
                userToUpdate.Avatar = Common.AssignStringValue(userToUpdate.Avatar, data.Avatar?.ToString());
                userToUpdate.BannerImage = Common.AssignStringValue(userToUpdate.BannerImage, data.BgThumbnail);
                userToUpdate.Bio = Common.AssignStringValue(userToUpdate.Bio, data.Bio);
                userToUpdate.FirstName = Common.AssignStringValue(userToUpdate.FirstName, data.FirstName);
                userToUpdate.LastName = Common.AssignStringValue(userToUpdate.LastName, data.LastName);
                userToUpdate.DateOfBirth = data.DateOfBirth ?? userToUpdate.DateOfBirth;
                userToUpdate.MaritalStatus = Common.AssignStringValue(userToUpdate.MaritalStatus, data.MaritalStatus);
                userToUpdate.Religion = Common.AssignStringValue(userToUpdate.Religion, data.Religion);
                userToUpdate.CountryOfOrigin = Common.AssignStringValue(userToUpdate.CountryOfOrigin, data.CountryOfOrigin);
                userToUpdate.Hobbies = data.Hobbies ?? userToUpdate.Hobbies ?? new string[] { };
                userToUpdate.IsCompleted = true;
                userToUpdate.UpdatedAt = DateTime.UtcNow;

                await supabase
                    .From<AlSaqrUser>()
                    .Where(u => u.Id == userToUpdate.Id)
                    .Upsert(userToUpdate);

                return userToUpdate.Id;

            }
            catch(CompleteRegistrationException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new CompleteRegistrationException(userId, ex);
            }
        }

        public async Task<Guid> DeleteUser(Supabase.Client client, Guid userId)
        {
            AlSaqrUser? userToDelete = (await client.From<AlSaqrUser>().Where(u => u.Id == userId).Single());
            if (userToDelete == null)
                throw new Exception("User not found");


            await RemoveUserPosts(client, userId);
            await RemoveCommunityMember(client, userId);
            await RemoveCommunityDiscussionMember(client, userId, Guid.Empty);

            await client.From<AlSaqrUser>().Where(u => u.Id == userId).Delete();
            return userId;
        }

        private async Task<bool> RemoveUserPosts(Supabase.Client client, Guid userId)
        {
            Guid currentPostBeingDeletedId = Guid.Empty;
            try
            {
                var userPosts = await client.From<Post>().Where(p => p.UserId == userId).Get();
                if (userPosts == null || userPosts.Models.Count == 0)
                    return true;
                foreach (var post in userPosts.Models)
                {
                    currentPostBeingDeletedId = post.Id;
                    await RemovePostStatuses(client, post.Id);
                    await client.From<Post>().Where(p => p.Id == post.Id).Delete();
                }
                return true;

            }
            catch (DeletePostException)
            {
                throw; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeletePostException(currentPostBeingDeletedId, ex); // wrap the real exception
            }
        }
        private async Task<bool> RemovePostStatuses(Supabase.Client client, Guid postId)
        {
            Guid currentPostStatusBeingDeletedId = Guid.Empty;
            try
            {
                var userPostStatuses = await client.From<PostStatus>().Where(ps => ps.PostId == postId).Get();
                if (userPostStatuses == null || userPostStatuses.Models.Count == 0)
                    return true;
                foreach (var postStatus in userPostStatuses.Models)
                {
                    currentPostStatusBeingDeletedId = postStatus.Id;
                    await client.From<PostStatus>().Where(ps => ps.Id == postStatus.Id).Delete();
                }
                return true;
            }
            catch (DeletePostStatusException)
            {
                throw; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeletePostStatusException(currentPostStatusBeingDeletedId, ex); // wrap the real exception
            }
        }
        private async Task<bool> RemoveCommunityMember(Supabase.Client client, Guid userId)
        {
            Guid currentCommunityMemberBeingDeletedId = Guid.Empty;
            Guid currentCommunityMemberCommunityId = Guid.Empty;
            try
            {
                var communityMemberLinks = await client.From<CommunityMember>().Where(cm => cm.UserId == userId).Get();
                if (communityMemberLinks == null || communityMemberLinks.Models.Count == 0)
                    return true;
                foreach (var communityMemberLink in communityMemberLinks.Models)
                {
                    currentCommunityMemberBeingDeletedId = communityMemberLink.Id;
                    currentCommunityMemberCommunityId = communityMemberLink.CommunityId;
                    await client.From<CommunityMember>().Where(cm => cm.Id == communityMemberLink.Id).Delete();
                }
                return true;
            }
            catch (DeleteCommunityMemberException)
            {
                throw; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeleteCommunityMemberException(
                            communityMemberId: currentCommunityMemberBeingDeletedId,
                            communityId: currentCommunityMemberCommunityId,
                            userId: userId,
                            innerException: ex); // wrap the real exception
            }
        }
        private async Task<bool> RemoveCommunityDiscussionMember(Supabase.Client client, Guid userId, Guid communityId)
        {
            Guid currentCommunityDiscussionMemberBeingDeletedId = Guid.Empty;
            Guid currentCommunityDiscussionMemberCommunityDiscussionId = Guid.Empty;

            try
            {
                var communityDiscussionMemberLinks = await client.From<CommunityDiscussionMember>().Where(cm => cm.UserId == userId).Get();
                if (communityDiscussionMemberLinks == null || communityDiscussionMemberLinks.Models.Count == 0)
                    return true;
                foreach (var communityDiscussionMemberLink in communityDiscussionMemberLinks.Models)
                {
                    currentCommunityDiscussionMemberBeingDeletedId = communityDiscussionMemberLink.Id;
                    currentCommunityDiscussionMemberCommunityDiscussionId = communityDiscussionMemberLink.CommunityDiscussionId;
                    await client.From<CommunityDiscussionMember>().Where(cdm => cdm.Id == communityDiscussionMemberLink.Id).Delete();
                }
                return true;
            }
            catch (DeleteCommunityDiscussionMemberException)
            {
                throw; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeleteCommunityDiscussionMemberException(
                            communityDiscussionMemberId: currentCommunityDiscussionMemberBeingDeletedId,
                            communityId: communityId,
                            communityDiscussionId: currentCommunityDiscussionMemberCommunityDiscussionId,
                            userId: userId,
                            innerException: ex); // wrap the real exception
            }
        }
    }
}
