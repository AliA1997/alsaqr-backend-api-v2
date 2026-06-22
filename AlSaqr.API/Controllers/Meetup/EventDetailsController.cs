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
        [HttpGet("{eventId}")]
        public async Task<IActionResult> GetEventDetails(Guid eventId)
        {
            var eventDetails = await _eventRepository.GetEventDetails(_supabase, eventId);

            return Ok(new { eventDetails, success = true });
        }

        /// <summary>
        /// Get nearby events by current event
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        [HttpGet("{eventId}/nearby")]
        public async Task<IActionResult> GetNearbyEventByCurrentEvent(
            Guid eventId,
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
