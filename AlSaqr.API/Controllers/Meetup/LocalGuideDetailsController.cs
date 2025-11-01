using AlSaqr.Data.Entities;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Newtonsoft.Json;
using Supabase.Postgrest.Interfaces;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class LocalGuideDetailsController : ControllerBase
    {
        private readonly ILogger<LocalGuidesController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly ILocalGuidesRepository _localGuidesRepository;
        public LocalGuideDetailsController(
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
        /// Get local guide details
        /// </summary>
        /// <param name="latitude"></param>
        /// <returns></returns>
        [HttpGet("{localGuideId}")]
        public async Task<IActionResult> GetLocalGuideDetails(
                int localGuideId
            )
        {
            await using var session = _driver.AsyncSession();

            LocalGuideDetailsDto? localGuideDetails = null;
            IPostgrestTable<VwLocalGuides>? selectLocalGuideResult = null;
            try
            {
                selectLocalGuideResult = _supabase.From<VwLocalGuides>()
                                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, localGuideId)
                                            .Limit(1);

                var localGuide = (await selectLocalGuideResult.Get()).Model;
                var userInfoResult = await Neo4jHelpers.ReadAsync(
                    session,
                    @"
                        MATCH (user:User {id: $userId}) 
                        RETURN user
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", localGuide.UserId },
                    },
                    new[] { "user" }
                );

                localGuideDetails = new LocalGuideDetailsDto()
                {
                    Id = localGuide.Id,
                    UserId = localGuide.UserId,
                    Name = localGuide.Name,
                    CitiesHosted = localGuide.CitiesHosted,
                    RegisteredAt = localGuide.RegisteredAt,
                    UserInfo = userInfoResult.Count() > 0 ? userInfoResult.First()["user"] : null
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
                int localGuideId,
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
