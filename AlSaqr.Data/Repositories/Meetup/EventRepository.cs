using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Newtonsoft.Json;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Events;
using static Supabase.Postgrest.Constants;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class EventRepository: IEventRepository
    {
        public EventRepository() { }

        public async Task<PaginatedResult<EventDto>> GetNearbyEvents(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var events = new List<EventDto>();
            var functionName = "get_nearby_events";
            var pagingFunctionName = "get_nearby_events_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetEventsParams(
                            latitude: latitude,
                            longitude: longitude,
                            skip: skip,
                            currentPage: currentPage,
                            itemsPerPage: itemsPerPage,
                            maxDistanceKm: null,
                            searchTerm: searchTerm
                );

                events = JsonConvert.DeserializeObject<List<EventDto>>(
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

            return new PaginatedResult<EventDto>(events ?? new List<EventDto>(), pagination!);
        }

        public async Task<PaginatedResult<EventDto>> GetNearbyOnlineEvents(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var events = new List<EventDto>();
            var functionName = "get_nearby_online_events";
            var pagingFunctionName = "get_nearby_online_events_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetEventsParams(
                            skip: skip,
                            latitude: latitude,
                            longitude: longitude,
                            currentPage: currentPage,
                            itemsPerPage: itemsPerPage,
                            maxDistanceKm: null,
                            searchTerm: string.IsNullOrEmpty(searchTerm) ? null : searchTerm
                );

                events = JsonConvert.DeserializeObject<List<EventDto>>(
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

            return new PaginatedResult<EventDto>(events ?? new List<EventDto>(), pagination!);
        }

        public async Task<PaginatedResult<EventDto>> GetMyEvents(
            Supabase.Client client,
            string userId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm)
        {
            var events = new List<EventDto>();
            var functionName = "get_my_events";
            var pagingFunctionName = "get_my_events_total";
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

                events = JsonConvert.DeserializeObject<List<EventDto>>(
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

            return new PaginatedResult<EventDto>(events ?? new List<EventDto>(), pagination!);
        }

        public async Task<Event> CreateEvent(
            Supabase.Client client,
            CreateEventForm form)
        {

            var recentInsertedId = await client.From<Event>().Count(CountType.Estimated);

            var model = new Event()
            {
                Id = recentInsertedId + 1,
                Name = form.Name,
                Description = form.Description,
                Images = form.Images ?? new Dictionary<string, object>[] { },
                GroupId = form.GroupId,
                IsOnline = form.IsOnline,
                TimesOccurred = 0,
                LastOccurredAt = form.DateToOccur,
                CreatedAt = DateTime.UtcNow
            };

            var insertedEvent = (await client.From<Event>().Upsert(model, new QueryOptions()
            {
                Returning = QueryOptions.ReturnType.Representation,
            })).Model;

            return insertedEvent!;
        }
    }
}
