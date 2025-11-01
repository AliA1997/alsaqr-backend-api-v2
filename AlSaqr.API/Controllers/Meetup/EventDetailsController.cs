using AlSaqr.Data.Entities.Meetup;
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

        public EventDetailsController(
            ILogger<EventDetailsController> logger,
            IDriver driver,
            Supabase.Client supabase,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _driver = driver;
            _supabase = supabase;
            _userCacheService = userCacheService;
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
    }
}
