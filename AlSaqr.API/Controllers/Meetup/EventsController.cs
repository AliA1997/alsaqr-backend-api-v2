using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class EventsController : AuthorizedControllerBase
    {
        private readonly ILogger<EventsController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly ICityRepository _cityRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IEventMemberRepository _eventMemberRepository;

        public EventsController(
            ILogger<EventsController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            ICityRepository cityRepository,
            IEventRepository eventRepository,
            IEventMemberRepository eventMemberRepository
        )
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _cityRepository = cityRepository;
            _eventRepository = eventRepository;
            _eventMemberRepository = eventMemberRepository;
        }

        /// <summary>
        /// Get events nearby
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <param name="maxDistanceKm"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetNearbyEvents(
            [FromQuery] string latitude,
            [FromQuery] string longitude,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null,
            [FromQuery] double? maxDistanceKm = 25.0
        )
        {
            var result = await _eventRepository.GetNearbyEvents(
                _supabase,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm,
                maxDistanceKm
            );

            return Ok(result);
        }

        /// <summary>
        /// Get online events nearby
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <param name="maxDistanceKm"></param>
        /// <returns></returns>
        [HttpGet("online")]
        public async Task<IActionResult> GetNearbyOnlineEvents(
            [FromQuery] string latitude,
            [FromQuery] string longitude,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null,
            [FromQuery] double? maxDistanceKm = 25.0
        )
        {
            var result = await _eventRepository.GetNearbyOnlineEvents(
                _supabase,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm,
                maxDistanceKm
            );

            return Ok(result);
        }

        /// <summary>
        /// Get my events nearby
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <param name="maxDistanceKm"></param>
        /// <returns></returns>
        [HttpGet("my")]
        public async Task<IActionResult> GetNearbyMyEvents(
            [FromQuery] string latitude,
            [FromQuery] string longitude,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null,
            [FromQuery] double? maxDistanceKm = 25.0
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var loggedInUser = _userCacheService.GetLoggedInUser();

            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("Need to be logged in to see your events or groups.");

            var result = await _eventRepository.GetMyEvents(
                _supabase,
                loggedInUser.Id.ToString(),
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm,
                maxDistanceKm
            );

            return Ok(result);
        }

        /// <summary>
        /// Create a event
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] AlSaqrUpsertRequest<CreateEventForm> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;

            if (
                string.IsNullOrEmpty(data.Name)
                || string.IsNullOrEmpty(data.Description)
                || data.GroupId == null
                || data.City == null
                || data.DateToOccur == null
            )
            {
                return BadRequest("Fields are required!");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();
                City? city = null;
                if (loggedInUser == null)
                {
                    return Unauthorized("User must be logged in, in order to create a event.");
                }
                Guid.TryParse(loggedInUser?.Id.ToString(), out var userId);

                city = await _cityRepository.InsertOrRetrieveCity(
                    _supabase,
                    data.City,
                    data.StateOrProvince,
                    data.Country,
                    data.Latitude,
                    data.Longitude
                );

                var insertedEvent = await _eventRepository.CreateEvent(userId, _supabase, data, ct);

                await _cityRepository.InsertCityEvent(_supabase, city.Id, insertedEvent.Id);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating event: {err.Message}");
                return StatusCode(500, new { message = "Add event error!", success = false });
            }
        }

        /// <summary>
        /// Update an event. Only the parent group's founder may update it.
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{eventId:guid}")]
        public async Task<IActionResult> UpdateEvent(
            Guid eventId,
            [FromBody] AlSaqrUpsertRequest<UpsertEventForm> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var data = request.Values;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to update an event.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                await _eventRepository.UpdateEvent(_supabase, eventId, userId, data, ct);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException err)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = err.Message, success = false }
                );
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error updating event: {err.Message}");
                return StatusCode(500, new { message = "Update event error!", success = false });
            }
        }

        /// <summary>
        /// Join an event as the logged-in user. Also joins the event's group
        /// when the user is not a member of it yet.
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        [HttpPut("{eventId:guid}/join")]
        public async Task<IActionResult> JoinEvent(Guid eventId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);
            if (userId == Guid.Empty)
                return Unauthorized("User must be logged in to join an event.");

            try
            {
                await _eventMemberRepository.JoinEvent(_supabase, userId, eventId, ct);

                _logger.LogInformation("User {userId} joined event {eventId}", userId, eventId);
                return Ok(new { success = true, message = "Joined Successfully" });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error joining event: {err.Message}");
                return StatusCode(500, new { message = "Join event error!", success = false });
            }
        }

        /// <summary>
        /// Remove a member from an event. Only the parent group's founder may do this;
        /// the member stays in the group.
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="memberUserId"></param>
        /// <returns></returns>
        [HttpDelete("{eventId:guid}/members/{memberUserId:guid}")]
        public async Task<IActionResult> RemoveEventMember(Guid eventId, Guid memberUserId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);
            if (userId == Guid.Empty)
                return Unauthorized("User must be logged in to remove an event member.");

            try
            {
                await _eventMemberRepository.RemoveEventMember(
                    _supabase,
                    userId,
                    eventId,
                    memberUserId,
                    ct
                );

                _logger.LogInformation(
                    "User {memberUserId} removed from event {eventId}",
                    memberUserId,
                    eventId
                );
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error removing event member: {err.Message}");
                return StatusCode(
                    500,
                    new { message = "Remove event member error!", success = false }
                );
            }
        }

        /// <summary>
        /// Delete an event. Only the parent group's founder may delete it.
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        [HttpDelete("{eventId:guid}")]
        public async Task<IActionResult> DeleteEvent(Guid eventId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to delete an event.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                await _eventRepository.DeleteEvent(_supabase, eventId, userId, ct);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException err)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = err.Message, success = false }
                );
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error deleting event: {err.Message}");
                return StatusCode(500, new { message = "Delete event error!", success = false });
            }
        }
    }
}
