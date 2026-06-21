using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Newtonsoft.Json;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class GroupRepository: IGroupRepository
    {

        public GroupRepository() 
        {
        }

        public async Task<PaginatedResult<GroupDto>> GetNearbyGroups(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var groups = new List<GroupDto>();
            var functionName = "get_nearby_groups";
            var pagingFunctionName = "get_nearby_groups_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetGroupsParams(
                            latitude: latitude,
                            longitude: longitude,
                            skip: skip,
                            currentPage: currentPage,
                            itemsPerPage: itemsPerPage,
                            maxDistanceKm: maxDistanceKm,
                            searchTerm: searchTerm
                );

                groups = JsonConvert.DeserializeObject<List<GroupDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

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

            return new PaginatedResult<GroupDto>(groups ?? new List<GroupDto>(), pagination!);

        }

        public async Task<PaginatedResult<GroupDto>> GetMyGroups(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string userId,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var groups = new List<GroupDto>();
            var functionName = "get_my_groups";
            var pagingFunctionName = "get_my_groups_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetMyEventsOrGroupsParams(
                            userId: userId,
                            latitude: latitude,
                            longitude: longitude,
                            skip: skip,
                            currentPage: currentPage,
                            itemsPerPage: itemsPerPage,
                            maxDistanceKm: null,
                            searchTerm: searchTerm
                );

                groups = JsonConvert.DeserializeObject<List<GroupDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

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

            return new PaginatedResult<GroupDto>(groups ?? new List<GroupDto>(), pagination!);
        }

        /// <summary>
        /// Gets the groups a user has joined, addressed by username (spec GJ-1..GJ-5).
        /// Sourced from vw_group_attendees. An unknown username or a user who has
        /// joined no groups yields an empty page rather than an error (GJ-5).
        /// </summary>
        public async Task<PaginatedResult<JoinedGroupDto>> GetJoinedGroups(
            Supabase.Client client,
            string username,
            int currentPage,
            int itemsPerPage,
            string? searchTerm)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var user = await client.From<AlSaqrUser>().Where(x => x.Username == username).Single();
            var userId = user.Id;

            var joinedGroups = new List<JoinedGroupDto>();
            Pagination pagination;
            var skip = (currentPage - 1) * itemsPerPage;
            var baseQuery = client.From<VwGroupAttendees>().Where(x => x.UserId == userId);
            var totalParams = new Dictionary<string, dynamic>()
            {
                { "p_user_id", userId.ToString() },
            };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                totalParams.Add("p_search_term", searchTerm);
                baseQuery = baseQuery.Filter("group_name", Operator.ILike, $"%{searchTerm}%");
            }

            var result = await SupabaseHelper.CallFunction(client, "get_profile_groups_count", totalParams);
            var totalItems = result != null ? long.Parse(result) : 0;


            if (totalItems == 0)
            {
                return new PaginatedResult<JoinedGroupDto>(
                    joinedGroups,
                    new Pagination
                    {
                        ItemsPerPage = itemsPerPage,
                        CurrentPage = currentPage,
                        TotalItems = 0,
                        TotalPages = 0
                    }
                );
            }

            joinedGroups = (await baseQuery.Range(skip, skip + itemsPerPage - 1).Get(ct))
                            .Models
                            .Select(vwJoinedGroup => new JoinedGroupDto(vwJoinedGroup))
                            .ToList();

            pagination = new Pagination
            {
                ItemsPerPage = itemsPerPage,
                CurrentPage = currentPage,
                TotalItems = (int)totalItems,
                TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
            };

            return new PaginatedResult<JoinedGroupDto>(joinedGroups, pagination);
        }

        public async Task<List<SimilarGroupDto>> GetSimilarGroups(
            Supabase.Client client,
            Guid groupId,
            string latitude,
            string longitude)
        {
            var similarGroups = new List<SimilarGroupDto>();
            var functionName = "get_similar_groups";
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetSimilarGroupsParams(
                            groupId: groupId,
                            latitude: latitude,
                            longitude: longitude
                );

                similarGroups = JsonConvert.DeserializeObject<List<SimilarGroupDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return similarGroups;
        }

        public async Task<Groups> CreateGroup(
            Supabase.Client client, 
            CreateGroupForm form, 
            Guid userId, 
            Guid organizerId, 
            Guid cityId,
            CancellationToken ct)
        {
            Groups? insertedGroup = null;
            try
            {

                //var recentInsertedId = await client.From<Groups>().Count(CountType.Estimated);
                //var recentInsertedGroupAttendee = await client.From<GroupAttendees>().Count(CountType.Estimated);

                var model = new Groups()
                {
                    Id = Guid.NewGuid(),
                    Name = form.Name,
                    Description = form.Description,
                    Images = form.Images ?? new string[] { },
                    HqCityId = cityId,
                    CreatedAt = DateTime.UtcNow
                };


                insertedGroup = (await client.From<Groups>().Upsert(model, new QueryOptions()
                {
                    Returning = QueryOptions.ReturnType.Representation,
                })).Model;

                await client.From<GroupAttendees>().Upsert(
                    new GroupAttendees()
                    {
                        Id = Guid.NewGuid(),
                        GroupId = insertedGroup.Id,
                        AttendeeId = organizerId,
                        IsGroupOrganizer = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    null, 
                    ct
                );
                
                await CreateGroupNotification(
                    client, 
                    organizerId,
                    insertedGroup.Id,
                    "Created group with a name of {group}",
                    "group_created",
                    ct
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating group in repository layer:", ex.Message);
            }
            finally
            {

            }
            return insertedGroup!;
        }

        private async Task CreateGroupNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid groupId,
            string messageTemplate,
            string notificationType, 
            CancellationToken ct)
        {
            var newGroup = await supabase
                .From<Groups>()
                .Where(c => c.Id == groupId)
                .Single(ct);

            if (newGroup == null)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{group}", newGroup.Name);

            var notification = new Notification
            {
                UserId = userId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "group",
                GroupId = groupId,
                Link = $"/groups/{groupId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = QueryOptions.ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }

    }
}
