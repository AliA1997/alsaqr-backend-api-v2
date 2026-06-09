using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
using AlSaqr.Domain.Utils;
using Supabase.Postgrest;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommunityDiscussionRepository: ICommunityDiscussionRepository
    {
        public CommunityDiscussionRepository() { }


        public async Task<PaginatedResult<CommunityDiscussionDto>> GetCommunityDiscussions(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var communityDiscussions = new List<CommunityDiscussionDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;



            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var baseQuery = supabase.From<VwCommunityDiscussionDetails>().Where(x => x.CommunityId == communityId);
                var totalParams = new Dictionary<string, dynamic>()
                {
                    { "p_community_id", communityId },
                };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    totalParams.Add("p_search_term", searchTerm);
                    baseQuery = baseQuery.Where(x => x.DiscussionTitle.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                    //baseQuery = baseQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_community_discussions_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<CommunityDiscussionDto>(
                        communityDiscussions,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }

                communityDiscussions = (await baseQuery.Order("discussion_created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwCommunity => new CommunityDiscussionDto(vwCommunity))
                                .ToList();

                communityDiscussions = await AssignUserRoles(supabase, userId, communityDiscussions, ct);

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = (int)totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<CommunityDiscussionDto>(communityDiscussions, pagination!);
        }

        public async Task<CommunityDiscussionDto> GetCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId)
        {

            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var communityDiscussionDetails = await supabase.From<VwCommunityDiscussionDetails>().Where(x => x.DiscussionId == communityDiscussionId).Single(ct);

                if (communityDiscussionDetails == null)
                {
                    throw new Exception("Community Discussion not found");
                }
                var result = await AssignUserRoles(supabase, userId, new List<CommunityDiscussionDto> { new CommunityDiscussionDto(communityDiscussionDetails) }, ct);
                return result.First();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        private async Task<List<CommunityDiscussionDto>> AssignUserRoles(
          Supabase.Client supabase,
          Guid userId,
          List<CommunityDiscussionDto> communityDiscussions,
          CancellationToken ct)
        {
            var communityDiscussionIds = communityDiscussions.Select(c => c.CommunityId).ToList();
            List<(Guid communityDiscussionId, string role)> result = new List<(Guid communityDiscussionId, string role)>();
            var userRoles = (await supabase.From<CommunityDiscussionMember>()
                                    .Where(cm => cm.UserId == userId)
                                    .Filter("community_discussion_id", Operator.In, communityDiscussionIds)
                                    .Get(ct))
                                    .Models.Select(cm => (cm.CommunityDiscussionId, cm.Role));

            foreach (var communityDiscussion in communityDiscussions)
            {
                if (userRoles.Any(cdm => cdm.CommunityDiscussionId == communityDiscussion.DiscussionId))
                {
                    communityDiscussion.RelationshipType = userRoles.First(ur => ur.CommunityDiscussionId == communityDiscussion.DiscussionId).Role;
                }
                else
                {
                    communityDiscussion.RelationshipType = "none";
                }
            }

            return communityDiscussions;
        }
        /// <summary>
        /// Retrieves admin information for a single community discussion: the
        /// discussion details, its founder, whether the caller is the founder,
        /// </summary>
        public async Task<AdminCommunityDiscussionInfoDto?> GetAdminCommunityDiscussionInfo(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId)
        {
            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                // 1. Discussion + founder + counts (one row from the view).
                var adminInfo = await supabase
                    .From<VwCommunityDiscussionAdminInfo>()
                    .Where(x => x.DiscussionId == communityDiscussionId)
                    .Single(ct);

                if (adminInfo == null)
                    throw new Exception($"Admin Community Discussion not found for id {communityDiscussionId}");

                // 2. isFounder is caller-specific, so compute it here rather than in the view.
                var isFounder = adminInfo.CreatorId == userId;

                if (!isFounder)
                    return null;

                // 3. Fetch the invite-requested users (the collection the view can't flatten).
                var requestedMembers = await supabase
                    .From<CommunityDiscussionMember>()
                    .Where(m => m.CommunityDiscussionId == communityDiscussionId)
                    .Filter("role", Operator.Equals, "requested")
                    .Get(ct);

                var requestedUserIds = requestedMembers.Models
                    .Select(m => m.UserId.ToString())
                    .ToList();

                var inviteRequestedUsers = new List<AlSaqrUser>();
                if (requestedUserIds.Any())
                {
                    var users = await supabase
                        .From<AlSaqrUser>()
                        .Filter("id", Operator.In, requestedUserIds)
                        .Get(ct);

                    inviteRequestedUsers = users.Models.ToList();
                }

                return new AdminCommunityDiscussionInfoDto(adminInfo, isFounder, inviteRequestedUsers.ToList<dynamic>());
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public async Task<Guid> CreateCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CreateCommunityDiscussionForm data)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {

                var communityDiscussion = new Entities.SocialMedia.CommunityDiscussion
                {

                    Id = Guid.NewGuid(),
                    CommunityId = communityId,
                    CreatorId = userId,
                    Title = data.Name,
                    Content = data.Description,
                    Tags = data.Tags ?? new string[] { },
                    IsPrivate = data.IsPrivate.ToLower() == "private",
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await supabase
                    .From<Entities.SocialMedia.CommunityDiscussion>()
                    .Insert(communityDiscussion, new QueryOptions
                    {
                        Returning = ReturnType.Representation
                    }, ct);

                if (inserted?.Model == null)
                    throw new Exception("Error creating community discussion");

                await CreateFounderCommunityDiscussionMember(supabase, inserted.Model.Id, userId, ct);

                await AddUsersToCommunityDiscussion(supabase, inserted.Model.Id, data.UsersAdded.ToList(), ct);

                await CreateCommunityDiscussionNotification(
                    supabase,
                    userId,
                    inserted.Model.Id,
                    "You created the community discussion of {communityDiscussion}",
                    "community_discussion_created",
                    ct);

                return inserted.Model.Id;
            }
            catch(CreateCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new CreateCommunityDiscussionException(communityId, ex);
            }

        }


        public async Task<Guid> UpdateCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            UpdateCommunityDiscussionForm data)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {
                // Check if the community discussion to update is the founder, if it isn't return an exception.
                Entities.SocialMedia.CommunityDiscussion? communityDiscussionToUpdate = await supabase.From<Entities.SocialMedia.CommunityDiscussion>()
                                                                                                        .Where(c => c.CreatorId == userId && c.Id == communityDiscussionId).Single();
                if (communityDiscussionToUpdate == null)
                    throw new Exception("Can't update the community discussion");

                communityDiscussionToUpdate.Title = Common.AssignStringValue(communityDiscussionToUpdate.Title, data?.Name);
                communityDiscussionToUpdate.Content = Common.AssignStringValue(communityDiscussionToUpdate.Content, data?.Description);
                communityDiscussionToUpdate.IsPrivate = data != null ? data.IsPrivate.ToLower() == "private" : communityDiscussionToUpdate.IsPrivate;
                communityDiscussionToUpdate.Tags = data?.Tags ?? communityDiscussionToUpdate.Tags;

                await supabase.From<Entities.SocialMedia.CommunityDiscussion>().Where(c => c.Id == communityDiscussionToUpdate.Id).Upsert(communityDiscussionToUpdate, null, ct);

                await CreateCommunityDiscussionNotification(
                    supabase,
                    userId,
                    communityDiscussionId,
                    "You updated the community discussion {communityDiscussion}",
                    "community_discussion_updated",
                    ct);

                return communityDiscussionToUpdate.Id;

            }
            catch (UpdateCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new UpdateCommunityDiscussionException(communityDiscussionId, ex);
            }
        }

        public async Task<Guid> DeleteCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {
                // Only the founder may delete the community.
                Entities.SocialMedia.CommunityDiscussion? communityToDelete = await supabase
                    .From<Entities.SocialMedia.CommunityDiscussion>()
                    .Where(c => c.Id == communityDiscussionId && c.CreatorId == userId)
                    .Single(ct);

                if (communityToDelete == null)
                    throw new Exception("Can't delete the community");

                // Remove dependent rows first.
                await supabase
                    .From<CommunityDiscussionMember>()
                    .Where(cm => cm.CommunityDiscussionId == communityDiscussionId)
                    .Delete(null, ct);

                await supabase
                    .From<Notification>()
                    .Where(n => n.CommunityDiscussionId == communityDiscussionId)
                    .Delete(null, ct);

                // Delete the community itself.
                await supabase
                    .From<Entities.SocialMedia.CommunityDiscussion>()
                    .Where(c => c.Id == communityDiscussionId)
                    .Delete(null, ct);

                await CreateCommunityDiscussionNotification(
                    supabase,
                    userId,
                    communityDiscussionId,
                    "You deleted the community discussion {communityDiscussion}",
                    "community_discussion_deleted",
                    ct);

                return communityDiscussionId;
            } catch(DeleteCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new DeleteCommunityDiscussionException(communityDiscussionId, userId, ex);
            }
        }
        public async Task<Guid> CreateCommunityDiscussionMessage(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            CreateCommunityDiscussionMessageForm data)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {
                var communityDiscussionMsg = new CommunityDiscussionMessage
                {

                    Id = Guid.NewGuid(),
                    CommunityDiscussionId = communityDiscussionId,
                    CreatorId = userId,
                    Content = data.Content,
                    Media = data.Media,
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await supabase
                    .From<CommunityDiscussionMessage>()
                    .Insert(communityDiscussionMsg, new QueryOptions
                    {
                        Returning = ReturnType.Representation
                    });

                if (inserted?.Model == null)
                    throw new Exception("Error creating community discussion");


                return inserted.Model.Id;
            }
            catch(CreateCommunityDiscussionMessageException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new CreateCommunityDiscussionMessageException(communityDiscussionId, ex);
            }

   
        }
        
        private async Task CreateFounderCommunityDiscussionMember(
            Supabase.Client supabase,
            Guid communityDiscussionId,
            Guid founderUserId,
            CancellationToken ct)
        {
            var founderMemberRecord = new CommunityDiscussionMember
            {
                Id = Guid.NewGuid(),
                CommunityDiscussionId = communityDiscussionId,
                UserId = founderUserId,
                Role = "founder",
                JoinedAt = DateTime.UtcNow
            };

            var inserted = await supabase
                .From<CommunityDiscussionMember>()
                .Insert(founderMemberRecord, new QueryOptions
                {
                    Returning = ReturnType.Representation
                }, ct);
            if (inserted == null)
                throw new Exception("Error adding founder member record to community discussion");

            return;
        }

        private async Task AddUsersToCommunityDiscussion(
            Supabase.Client supabase,
            Guid communityDiscussionId,
            List<Guid> userIds,
            CancellationToken ct)
        {
            var members = userIds.Select(userId => new CommunityDiscussionMember
            {
                Id = Guid.NewGuid(),
                CommunityDiscussionId = communityDiscussionId,
                UserId = userId,
                Role = "member",
                JoinedAt = DateTime.UtcNow
            }).ToList();

            var inserted = await supabase
                .From<CommunityDiscussionMember>()
                .Insert(members, new QueryOptions
                {
                    Returning = ReturnType.Representation
                }, ct);
            if (inserted == null)
                throw new Exception("Error adding members to community discussion");

            return;
        }


        private async Task CreateCommunityDiscussionNotification(
          Supabase.Client supabase,
          Guid userId,
          Guid communityDiscussionId,
          string messageTemplate,
          string notificationType,
          CancellationToken ct)
        {
            var communityDiscussion = await supabase
                .From<Entities.SocialMedia.CommunityDiscussion>()
                .Where(c => c.Id == communityDiscussionId)
                .Single(ct);

            if (communityDiscussion == null || communityDiscussion.CreatorId != userId)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{communityDiscussion}", communityDiscussion.Title);

            var notification = new Notification
            {
                UserId = communityDiscussion.CreatorId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "community_discussion",
                CommunityDiscussionId = communityDiscussionId,
                Link = $"/communityDiscussions/{communityDiscussionId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
