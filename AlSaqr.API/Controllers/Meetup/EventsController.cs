using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly ILogger<EventsController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly ICityRepository _cityRepository;
        private readonly IEventRepository _eventRepository;

        public EventsController(
            ILogger<EventsController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            ICityRepository cityRepository,
            IEventRepository eventRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _cityRepository = cityRepository;
            _eventRepository = eventRepository;
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
                maxDistanceKm);

            return Ok(result);
        }


        /// <summary>
        /// Create a event
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] AlSaqrUpsertRequest<CreateEventForm> request)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;

            if (string.IsNullOrEmpty(data.Name) || string.IsNullOrEmpty(data.Description) || data.GroupId == null 
                || data.City == null || data.DateToOccur == null)
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
                
                city = await _cityRepository.InsertOrRetrieveCity(_supabase, data.City, data.StateOrProvince, data.Country, data.Latitude, data.Longitude);


                var insertedEvent = await _eventRepository.CreateEvent(userId, _supabase, data, ct);

                //await _cityRepository.InsertCityEvent(_supabase, city.Id, insertedEvent.Id);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating event: {err.Message}");
                return StatusCode(500, new { message = "Add event error!", success = false });
            }
        }

    }
}
