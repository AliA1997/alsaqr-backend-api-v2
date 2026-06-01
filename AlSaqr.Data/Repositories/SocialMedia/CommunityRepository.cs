
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.Utils;
using Supabase.Postgrest;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommunityRepository : ICommunityRepository
    {
        public CommunityRepository() { }

        public async Task<PaginatedResult<CommunityDto>> GetCommunities(
            Supabase.Client supabase,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var communities = new List<CommunityDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;


            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var baseQuery = supabase.From<VwCommunityDetails>().Where(x => x.CommunityId != null);
                var totalParams = new Dictionary<string, dynamic>();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    totalParams.Add("p_search_term", searchTerm);
                    baseQuery = baseQuery.Where(x => x.CommunityName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                    //baseQuery = baseQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_community_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<CommunityDto>(
                        communities,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                communities = (await baseQuery.Order("community_created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwCommunity => new CommunityDto(vwCommunity))
                                .ToList();

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

            return new PaginatedResult<CommunityDto>(communities, pagination!);
        }

        public async Task<CommunityDto> GetCommunity(
            Supabase.Client supabase,
            Guid commmunityId)
        {

            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var communityDetails = await supabase.From<VwCommunityDetails>().Where(x => x.CommunityId == commmunityId).Single(ct);

                if(communityDetails == null)
                {
                    throw new Exception("Community not found");
                }

                return new CommunityDto(communityDetails);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public async Task<Guid> CreateCommunity(
              Supabase.Client supabase,
              Guid userId,
              CreateCommunityFormDto data)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var community = new Community
            {
                FounderId = userId,
                Name = data.Name,
                Description = data.Description,
                Avatar = data.AvatarOrBannerImage,
                Tags = data.Tags ?? Array.Empty<string>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var inserted = await supabase
                .From<Community>()
                .Insert(community, new QueryOptions
                {
                    Returning = ReturnType.Representation
                }, ct);

            if (inserted?.Model == null)
                throw new Exception("Error creating community");
            var communityId = inserted.Model.Id;

            await AddUsersToCommunity(supabase, communityId, data.UsersAdded.ToList(), ct);

            await CreateCommunityNotification(
                supabase,
                userId,
                communityId,
                "You created the community of {community}",
                "community_created",
                ct);

            return communityId;
        }


        public async Task<Guid> UpdateCommunity(
            Supabase.Client supabase, 
            Guid userId, 
            Guid communityId,
            UpdateCommunityForm updatedCommunity)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            // Check if the community to update is the founder, if it isn't return an exception.
            Community? communityToUpdate = (await supabase.From<Community>()
                                                        .Where(c => c.FounderId == userId && c.Id == communityId).Single());
            if (communityToUpdate == null)
                throw new Exception("Can't update the community");

            communityToUpdate.Name = Common.AssignStringValue(communityToUpdate.Name, updatedCommunity?.Name);
            communityToUpdate.Description = Common.AssignStringValue(communityToUpdate.Description, updatedCommunity?.Description);
            communityToUpdate.Avatar = Common.AssignStringValue(communityToUpdate.Avatar, updatedCommunity?.Avatar);
            communityToUpdate.Tags = updatedCommunity?.Tags ?? new string[] { };

            await supabase.From<Community>().Where(c => c.Id == communityToUpdate.Id).Upsert(communityToUpdate, null, ct);

            await CreateCommunityNotification(
                supabase,
                userId,
                communityId,
                "You updated the community {community}",
                "community_updated",
                ct);

            return communityToUpdate.Id;
        }

        /// <summary>
        /// Deletes a community. Only the founder may delete it. Removes all
        /// member rows and the community's notifications first to avoid orphans,
        /// then deletes the community record.
        /// </summary>
        public async Task<Guid> DeleteCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            // Only the founder may delete the community.
            Community? communityToDelete = await supabase
                .From<Community>()
                .Where(c => c.Id == communityId && c.FounderId == userId)
                .Single(ct);

            if (communityToDelete == null)
                throw new Exception("Can't delete the community");

            // Remove dependent rows first.
            await supabase
                .From<CommunityMember>()
                .Where(cm => cm.CommunityId == communityId)
                .Delete(null, ct);

            await supabase
                .From<Notification>()
                .Where(n => n.CommunityId == communityId)
                .Delete(null, ct);

            // Delete the community itself.
            await supabase
                .From<Community>()
                .Where(c => c.Id == communityId)
                .Delete(null, ct);

            await CreateCommunityNotification(
                supabase,
                userId,
                communityId,
                "You deleted the community {community}",
                "community_deleted",
                ct);

            return communityId;
        }

        private async Task AddUsersToCommunity(
            Supabase.Client supabase, 
            Guid communityId, 
            List<Guid> userIds,
            CancellationToken ct)
        {
            var members = userIds.Select(userId => new CommunityMember
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                UserId = userId,
                Role = "member",
                JoinedAt = DateTime.UtcNow
            }).ToList();

            var inserted = await supabase
                .From<CommunityMember>()
                .Insert(members, new Supabase.Postgrest.QueryOptions
                {
                    Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
                }, ct);
            if (inserted == null)
                throw new Exception("Error adding members to community");

            return;
        }

        private async Task CreateCommunityNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            string messageTemplate,
            string notificationType, 
            CancellationToken ct)
        {
            var community = await supabase
                .From<Community>()
                .Where(c => c.Id == communityId)
                .Single(ct);

            if (community == null || community.FounderId == userId)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{community}", community.Name);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = community.FounderId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "community",
                CommunityId = communityId,
                Link = $"/communities/{communityId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
