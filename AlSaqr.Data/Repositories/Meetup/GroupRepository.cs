using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Neo4j.Driver;
using Newtonsoft.Json;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Groups;
using static Supabase.Postgrest.Constants;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        public async Task<Groups> CreateGroup(Supabase.Client client, CreateGroupForm form, string neo4jUserId, int organizerId, int cityId)
        {
            Groups? insertedGroup = null;
            try
            {

                var recentInsertedId = await client.From<Groups>().Count(CountType.Estimated);
                var recentInsertedGroupAttendee = await client.From<GroupAttendees>().Count(CountType.Estimated);

                var model = new Groups()
                {
                    Id = recentInsertedId + 1,
                    Name = form.Name,
                    Description = form.Description,
                    Images = form.Images ?? new Dictionary<string, object>[] { },
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
                        Id = recentInsertedGroupAttendee + 1,
                        GroupId = insertedGroup.Id,
                        AttendeeId = organizerId,
                        IsGroupOrganizer = true,
                        CreatedAt = DateTime.UtcNow
                    }
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
    }
}
