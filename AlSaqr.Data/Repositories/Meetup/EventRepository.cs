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
                            maxDistanceKm: maxDistanceKm,
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


        public async Task<PaginatedResult<AttendedEventDto>> GetAttendedEvents(
            Supabase.Client client,
            string username,
            int currentPage,
            int itemsPerPage,
            string? searchTerm)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            
            var user = await client.From<AlSaqrUser>().Where(x => x.Username == username).Single(ct);
            var userId = user.Id;
            
            var attendedEvents = new List<AttendedEventDto>();
            Pagination pagination;
            var skip = (currentPage - 1) * itemsPerPage;
            

            var baseQuery = client.From<VwEventAttendees>().Where(x => x.UserId == userId);

            var totalParams = new Dictionary<string, dynamic>()
            {
                { "p_user_id", userId.ToString() },
            };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                totalParams.Add("p_search_term", searchTerm);
                baseQuery = baseQuery.Filter("event_name", Operator.ILike, $"%{searchTerm}%");
            }
            var result = await SupabaseHelper.CallFunction(client, "get_profile_events_count", totalParams);
            var totalItems = result != null ? long.Parse(result) : 0;


            if (totalItems == 0)
            {
                return new PaginatedResult<AttendedEventDto>(
                    attendedEvents,
                    new Pagination
                    {
                        ItemsPerPage = itemsPerPage,
                        CurrentPage = currentPage,
                        TotalItems = 0,
                        TotalPages = 0
                    }
                );
            }

            attendedEvents = (await baseQuery.Range(skip, skip + itemsPerPage - 1).Get(ct))
                            .Models
                            .Select(vwAttendedEvent => new AttendedEventDto(vwAttendedEvent))
                            .ToList();

            pagination = new Pagination
            {
                ItemsPerPage = itemsPerPage,
                CurrentPage = currentPage,
                TotalItems = (int)totalItems,
                TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
            };

            return new PaginatedResult<AttendedEventDto>(attendedEvents, pagination);
        }

        public async Task<Event> CreateEvent(
            Guid userId,
            Supabase.Client client,
            CreateEventForm form,
            CancellationToken ct)
        {
            var model = new Event()
            {
                Id = Guid.NewGuid(),
                Name = form.Name,
                Description = form.Description,
                Images = form.Images ?? new string[] { },
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

            await CreateEventNotification(
                client,
                userId,
                insertedEvent.Id,
                "Started a new event with a name of {event}",
                "event",
                ct
            );

            return insertedEvent!;
        }
    
        private async Task CreateEventNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            string messageTemplate,
            string notificationType, 
            CancellationToken ct)
        {
            var newEvent = await supabase
                .From<Event>()
                .Where(c => c.Id == eventId)
                .Single(ct);

            if (newEvent == null)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate.Replace("{event}", newEvent.Name);

            var notification = new Notification
            {
                UserId = userId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "event",
                EventId = eventId,
                Link = $"/events/{eventId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = QueryOptions.ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }

    }
}
