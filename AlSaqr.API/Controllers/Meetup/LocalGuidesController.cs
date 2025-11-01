using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class LocalGuidesController : ControllerBase
    {
        private readonly ILogger<LocalGuidesController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly ILocalGuidesRepository _localGuidesRepository;

        public LocalGuidesController(
            ILogger<LocalGuidesController> logger,
            IDriver driver,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            ILocalGuidesRepository localGuidesRepository)
        {
            _logger = logger;
            _driver = driver;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _localGuidesRepository = localGuidesRepository;
        }


        /// <summary>
        /// Get local guides nearby
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <param name="maxDistanceKm"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetNearbyLocalGuides(
                [FromQuery] string latitude,
                [FromQuery] string longitude,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null,
                [FromQuery] double? maxDistanceKm = 25.0
            )
        {
            var result = await _localGuidesRepository.GetNearbyLocalGuides(
                _supabase,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm,
                maxDistanceKm);

            return Ok(result);
        }


    }
}
