using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class LocalGuideDetailsController : ControllerBase
    {
        private readonly ILogger<LocalGuidesController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly ILocalGuidesRepository _localGuidesRepository;
        public LocalGuideDetailsController(
            ILogger<LocalGuidesController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            ILocalGuidesRepository localGuidesRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _localGuidesRepository = localGuidesRepository;
        }

        /// <summary>
        /// Get local guide details
        /// </summary>
        /// <param name="latitude"></param>
        /// <returns></returns>
        [HttpGet("{localGuideId}")]
        public async Task<IActionResult> GetLocalGuideDetails(Guid localGuideId)
        {

            var result = await _localGuidesRepository.GetLocalGuideDetails(_supabase, localGuideId);

            return Ok(new { localGuideDetails = result, success = true });
        }


        /// <summary>
        /// Get local guides nearby
        /// </summary>
        /// <param name="localGuideId"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <returns></returns>
        [HttpGet("{localGuideId}/nearby")]
        public async Task<IActionResult> GetNearbyLocalGuidesForCurrentLocalGuide(
                Guid localGuideId,
                [FromQuery] string latitude,
                [FromQuery] string longitude
            )
        {
            var result = await _localGuidesRepository.GetNearbyLocalGuidesForCurrentLocalGuide(
                _supabase,
                localGuideId,
                latitude,
                longitude,
                currentPage: 1,
                itemsPerPage: 10,
                searchTerm: null,
                maxDistanceKm: null);

            var similarLocalGuides = result.Items;

            return Ok(new { similarLocalGuides });
        }

    }
}
