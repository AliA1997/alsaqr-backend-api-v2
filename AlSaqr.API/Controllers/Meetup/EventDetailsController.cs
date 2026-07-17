using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class EventDetailsController : ControllerBase
    {
        private readonly ILogger<EventDetailsController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly IEventRepository _eventRepository;

        public EventDetailsController(
            ILogger<EventDetailsController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            IEventRepository eventRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _eventRepository = eventRepository;
        }

        /// <summary>
        /// Get event details
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        [HttpGet("{eventSlug}")]
        public async Task<IActionResult> GetEventDetails(string eventSlug)
        {
            // Anonymous callers have no member to exclude, so an empty id excludes no one.
            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            var eventDetails = await _eventRepository.GetEventDetails(_supabase, eventSlug, userId);

            return Ok(new { eventDetails, success = true });
        }

        /// <summary>
        /// Get the members attending an event
        /// </summary>
        /// <param name="eventSlug"></param>
        /// <returns></returns>
        [HttpGet("{eventSlug}/members")]
        public async Task<IActionResult> GetEventMembers(
            string eventSlug,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            // Anonymous callers have no member to exclude, so an empty id excludes no one.
            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            var result = await _eventRepository.GetEventMembers(
                _supabase,
                eventSlug: eventSlug,
                userId: userId,
                currentPage: currentPage,
                itemsPerPage: itemsPerPage,
                searchTerm: searchTerm);

            var eventMembers = result.Items;
            var pagination = result.Pagination;

            return Ok(new { eventMembers, pagination, success = true });
        }

        /// <summary>
        /// Get nearby events by current event
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        [HttpGet("{eventSlug}/nearby")]
        public async Task<IActionResult> GetNearbyEventByCurrentEvent(
            string eventSlug,
            [FromQuery] string latitude,
            [FromQuery] string longitude)
        {
            var result = await _eventRepository.GetNearbyEvents(
                _supabase,
                latitude: latitude,
                longitude: longitude,
                currentPage: 1,
                itemsPerPage: 10,
                searchTerm: null,
                maxDistanceKm: null
             );

            var similarEvents = result.Items;

            return Ok(new { similarEvents, success = true });
        }
    }
}
