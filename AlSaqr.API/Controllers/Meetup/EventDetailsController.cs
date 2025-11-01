using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Supabase.Postgrest.Interfaces;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class EventDetailsController : ControllerBase
    {
        private readonly ILogger<EventDetailsController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly IEventRepository _eventRepository;

        public EventDetailsController(
            ILogger<EventDetailsController> logger,
            IDriver driver,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            IEventRepository eventRepository)
        {
            _logger = logger;
            _driver = driver;
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
        public async Task<IActionResult> GetEventDetails(int eventId)
        {
            EventDto? eventDetails = null;
            IPostgrestTable<VwEvent>? selectEventResult = null;
            try
            {
                selectEventResult = _supabase.From<VwEvent>()
                                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, eventId)
                                        .Limit(10);

                eventDetails = (await selectEventResult.Get()).Models.Select(sr => new EventDto()
                {
                    Id = sr.Id,
                    Slug = sr.Slug,
                    GroupId = sr.GroupId,
                    GroupName = sr.GroupName,
                    Name = sr.Name,
                    Description = sr.Description,
                    CitiesHosted = sr.CitiesHosted,
                    Images = sr.Images,
                    DistanceKm = 0
                }).First();

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(new { eventDetails, success = true });
        }

        /// <summary>
        /// Get nearby events by current event
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        [HttpGet("{eventId}/nearby")]
        public async Task<IActionResult> GetNearbyEventByCurrentEvent(
            int eventId,
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
