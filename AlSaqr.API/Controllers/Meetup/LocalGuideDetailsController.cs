using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest.Interfaces;

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
        public async Task<IActionResult> GetLocalGuideDetails(
                Guid localGuideId
            )
        {
            LocalGuideDetailsDto? localGuideDetails = null;
            IPostgrestTable<VwLocalGuides>? selectLocalGuideResult = null;
            try
            {
                selectLocalGuideResult = _supabase.From<VwLocalGuides>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, localGuideId.ToString())
                                            .Limit(1);

                var localGuide = (await selectLocalGuideResult.Get()).Model;

                localGuideDetails = new LocalGuideDetailsDto()
                {
                    Id = localGuide.Id,
                    UserId = localGuide.UserId,
                    Name = localGuide.Name,
                    CitiesHosted = localGuide.CitiesHosted,
                    RegisteredAt = localGuide.RegisteredAt,
                    UserInfo = null
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(new { localGuideDetails, success = true });
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
