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

        public async Task<(Guid userId, string username)> GetUserIdAndUsernameByEmail(Supabase.Client supabase, string email)
        {
            try
            {
                var userInfo = await supabase.From<AlSaqrUser>().Where(u => u.Email == email).Single();
                if (userInfo == null)
                    throw new Exception("User not found");

                return (userInfo.Id, userInfo.Username);
            } catch(Exception ex)
            {
                throw ex;
            }
        }

        public async Task<AlSaqrUser?> GetUserByEmail(Supabase.Client supabase, string email)
        {
            return (await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Email == email)
                .Limit(1)
                .Get()).Models.FirstOrDefault();
        }

        public async Task<AlSaqrUser?> GetUserByWeb3Address(Supabase.Client supabase, string web3Address)
        {
            return (await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Web3Address == web3Address)
                .Limit(1)
                .Get()).Models.FirstOrDefault();
        }

        /// <summary>
        /// Sets the user's web3 address when a new one is passed in, and returns the
        /// address currently stored on the record (set-and-retrieve for the session).
        /// </summary>
        public async Task<string?> SetWeb3Address(
            Supabase.Client supabase,
            Guid userId,
            string? web3Address,
            CancellationToken ct)
        {
            try
            {
                AlSaqrUser? user = await supabase.From<AlSaqrUser>().Where(u => u.Id == userId).Single(ct);
                if (user == null)
                    throw new Exception("User not found");

                if (!string.IsNullOrEmpty(web3Address) && user.Web3Address != web3Address)
                {
                    user.Web3Address = web3Address;
                    user.UpdatedAt = DateTime.UtcNow;

                    await supabase.From<AlSaqrUser>().Where(u => u.Id == userId).Upsert(user, null, ct);
                }

                return user.Web3Address;
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

        public async Task<PaginatedResult<UserToAdd>> GetUsersToAdd(
            Supabase.Client supabase, 
            Guid userGuid, 
            string? searchTerm, 
            int currentPage, 
            int itemsPerPage)
        {
            var usersToAdd = new List<UserToAdd>();
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

                var usersToAddDtos = JsonConvert.DeserializeObject<List<UsersToAddDto>>(
                    await SupabaseHelper.CallFunction(supabase, functionName, functionParams)
                ) ?? new List<UsersToAddDto>();
                usersToAdd = usersToAddDtos.Select(u => new UserToAdd(u)).ToList();

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

            return new PaginatedResult<UserToAdd>(usersToAdd, pagination!);

        }

        public async Task<PaginatedResult<PostsToAdd>> GetPostsToAdd(
            Supabase.Client supabase,
            Guid userGuid,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var postsToAdd = new List<PostsToAdd>();
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

                var postsToAddDto = JsonConvert.DeserializeObject<List<PostsToAddDto>>(
                    await SupabaseHelper.CallFunction(supabase, functionName, functionParams)
                ) ?? new List<PostsToAddDto>();
                postsToAdd = postsToAddDto.Select(p => new PostsToAdd(p)).ToList();

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

            return new PaginatedResult<PostsToAdd>(postsToAdd, pagination!);
        }
        public async Task<AlSaqrUser> CreateInitialUser(Supabase.Client client, CreateInitialUserDto newUser)
        {

            var newUserEntity = new AlSaqrUser()
            {
                Id = newUser.Id,
                Username = newUser.Username,
                FirstName = newUser.FirstName,
                LastName = newUser.LastName,
                Avatar = newUser.Avatar.ToString(),
                BannerImage = newUser.BgThumbnail,
                DateOfBirth = newUser.DateOfBirth,
                Email = newUser.Email,
                CreatedAt = newUser.CreatedAt,
                Hobbies = newUser.Hobbies,
                MaritalStatus = newUser.MaritalStatus,
                PreferredMadhab = newUser.PreferredMadhab,
                IslamicStudyTopics = newUser.IslamicStudyTopics,
                FavoriteIslamicScholars = newUser.FavoriteIslamicScholars,
                FavoriteQuranReciters = newUser.FavoriteQuranReciters,
                Web3Address = newUser.Web3Address,
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
                userToUpdate.CountryOfOrigin = Common.AssignStringValue(userToUpdate!.CountryOfOrigin, updatedUser?.CountryOfOrigin);
                userToUpdate.Hobbies = updatedUser?.Hobbies ?? new string[] { };
                userToUpdate.MaritalStatus = Common.AssignStringValue(userToUpdate.MaritalStatus, updatedUser?.MaritalStatus);
                userToUpdate.PreferredMadhab = !string.IsNullOrEmpty(userToUpdate.PreferredMadhab) ? userToUpdate.PreferredMadhab 
                                                    :  userToUpdate.PreferredMadhab?.ToString() == string.Empty ? null 
                                                        : updatedUser?.PreferredMadhab; // Go back to original value, if value is invalid. 

                userToUpdate.IslamicStudyTopics = updatedUser?.IslamicStudyTopics ?? new string[] { };
                userToUpdate.FavoriteIslamicScholars = updatedUser?.FavoriteIslamicScholars ?? new string[] { }; 
                userToUpdate.FavoriteQuranReciters = updatedUser?.FavoriteQuranReciters ?? new string[] { };

                await client.From<AlSaqrUser>().Where(u => u.Id == userToUpdate!.Id).Upsert(userToUpdate, null, ct);
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
            UserRegisterFormDto data,
            CancellationToken ct)
        {
            try
            {
                // Web3 flow: when the email already belongs to another existing account,
                // just record the web3 address on that record instead of registering anew.
                if (!string.IsNullOrEmpty(data.Email))
                {
                    var existingByEmail = (await supabase
                        .From<AlSaqrUser>()
                        .Where(u => u.Email == data.Email)
                        .Limit(1)
                        .Get(ct)).Models.FirstOrDefault();

                    if (existingByEmail != null && existingByEmail.Id != userId)
                    {
                        await SetWeb3Address(supabase, existingByEmail.Id, data.Web3Address, ct);
                        return existingByEmail.Id;
                    }
                }

                AlSaqrUser? userToUpdate = await supabase
                    .From<AlSaqrUser>()
                    .Where(u => u.Id == userId)
                    .Single();

                if (userToUpdate == null)
                    throw new Exception("User not found");

                userToUpdate.Email = Common.AssignStringValue(userToUpdate.Email, data.Email);
                userToUpdate.Web3Address = Common.AssignStringValue(userToUpdate.Web3Address, data.Web3Address);
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
                    .Upsert(userToUpdate, null, ct);

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

            await RemoveLists(client, userId);
            await RemoveListItems(client, userId);

            await RemoveCommunity(client, userId);
            await RemoveCommunityMember(client, userId);
            await RemoveCommunityDiscussion(client, userId);
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
            catch (DeletePostException ex)
            {
                throw ex; // re-throw if it's already the right type
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
            catch (DeletePostStatusException ex)
            {
                throw ex; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeletePostStatusException(currentPostStatusBeingDeletedId, ex); // wrap the real exception
            }
        }
        private async Task<bool> RemoveListItems(Supabase.Client client, Guid userId)
        {
            Guid currentListItemBeingDeletedId = Guid.Empty;
            try
            {
                var listItemsToDelete = await client.From<Entities.SocialMedia.ListItem>().Where(l => l.UserId == userId).Get();
                if (listItemsToDelete == null || listItemsToDelete.Models.Count == 0)
                    return true;
                foreach (var listItemToDelete in listItemsToDelete.Models)
                {
                    currentListItemBeingDeletedId = listItemToDelete.Id;
                    await client.From<Entities.SocialMedia.ListItem>().Where(cm => cm.Id == listItemToDelete.Id).Delete();
                }
                return true;
            }
            catch (DeleteListItemException ex)
            {
                throw ex; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeleteListItemException(
                            listItemId: currentListItemBeingDeletedId,
                            innerException: ex); // wrap the real exception
            }
        }
        private async Task<bool> RemoveLists(Supabase.Client client, Guid userId)
        {
            Guid currentListBeingDeletedId = Guid.Empty;
            try
            {
                var listsToDelete = await client.From<Entities.SocialMedia.List>().Where(l => l.UserId == userId).Get();
                if (listsToDelete == null || listsToDelete.Models.Count == 0)
                    return true;
                foreach (var listToDelete in listsToDelete.Models)
                {
                    currentListBeingDeletedId = listToDelete.Id;
                    await client.From<Entities.SocialMedia.List>().Where(cm => cm.Id == listToDelete.Id).Delete();
                }
                return true;
            }
            catch (DeleteListException ex)
            {
                throw ex; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeleteListException(
                            listId: currentListBeingDeletedId,
                            innerException: ex); // wrap the real exception
            }
        }
        private async Task<bool> RemoveCommunity(Supabase.Client client, Guid userId)
        {
            Guid currentCommunityBeingDeletedId = Guid.Empty;
            try
            {
                var communitiesToDelete = await client.From<Entities.SocialMedia.Community>().Where(cm => cm.FounderId == userId).Get();
                if (communitiesToDelete == null || communitiesToDelete.Models.Count == 0)
                    return true;
                foreach (var communityToDelete in communitiesToDelete.Models)
                {
                    currentCommunityBeingDeletedId = communityToDelete.Id;
                    await client.From<Entities.SocialMedia.Community>().Where(cm => cm.Id == communityToDelete.Id).Delete();
                }
                return true;
            }
            catch (DeleteCommunityException ex)
            {
                throw ex; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeleteCommunityException(
                            communityId: currentCommunityBeingDeletedId,
                            userId: userId,
                            innerException: ex); // wrap the real exception
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
            catch (DeleteCommunityMemberException ex)
            {
                throw ex; // re-throw if it's already the right type
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
        private async Task<bool> RemoveCommunityDiscussion(Supabase.Client client, Guid userId)
        {
            Guid currentCommunityDiscussionBeingDeletedId = Guid.Empty;
            try
            {
                var communityDiscussionsToDelete = await client.From<Entities.SocialMedia.CommunityDiscussion>().Where(cd => cd.CreatorId == userId).Get();
                if (communityDiscussionsToDelete == null || communityDiscussionsToDelete.Models.Count == 0)
                    return true;
                foreach (var communityDiscussionToDelete in communityDiscussionsToDelete.Models)
                {
                    currentCommunityDiscussionBeingDeletedId = communityDiscussionToDelete.Id;
                    await client.From<Entities.SocialMedia.CommunityDiscussion>().Where(cm => cm.Id == communityDiscussionToDelete.Id).Delete();
                }
                return true;
            }
            catch (DeleteCommunityDiscussionException ex)
            {
                throw ex; // re-throw if it's already the right type
            }
            catch (Exception ex)
            {
                throw new DeleteCommunityDiscussionException(
                            communityDiscussionId: currentCommunityDiscussionBeingDeletedId,
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
            catch (DeleteCommunityDiscussionMemberException ex)
            {
                throw ex; // re-throw if it's already the right type
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
