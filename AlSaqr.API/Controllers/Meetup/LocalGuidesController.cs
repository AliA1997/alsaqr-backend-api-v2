using AlSaqr.Data.Repositories.Meetup;
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
    public class LocalGuidesController : AuthorizedControllerBase
    {
        private readonly ILogger<LocalGuidesController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly ILocalGuidesRepository _localGuidesRepository;
        private readonly ICityRepository _cityRepository;

        public LocalGuidesController(
            ILogger<LocalGuidesController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            ILocalGuidesRepository localGuidesRepository,
            ICityRepository cityRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _localGuidesRepository = localGuidesRepository;
            _cityRepository = cityRepository;
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

        /// <summary>
        /// Register the logged-in user as a local guide.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateLocalGuide(
            [FromBody] AlSaqrUpsertRequest<CreateLocalGuideForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var data = request.Values;

            if (string.IsNullOrEmpty(data?.Name))
                return BadRequest("Name is required.");

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to register as a local guide.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                var cityIds = await ResolveCityIds(data.Cities);
                await _localGuidesRepository.CreateLocalGuide(_supabase, userId, data, cityIds, ct);
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error creating local guide: {err.Message}");
                return StatusCode(500, new { message = "Add local guide error!", success = false });
            }
        }

        /// <summary>
        /// Update a local guide. A user may only update their own local guide record.
        /// </summary>
        /// <param name="localGuideId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{localGuideId:guid}")]
        public async Task<IActionResult> UpdateLocalGuide(
            Guid localGuideId,
            [FromBody] AlSaqrUpsertRequest<UpsertLocalGuideForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var data = request.Values;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to update a local guide.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                var cityIds = await ResolveCityIds(data.Cities);
                await _localGuidesRepository.UpdateLocalGuide(_supabase, localGuideId, userId, data, cityIds, ct);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException err)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = err.Message, success = false });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error updating local guide: {err.Message}");
                return StatusCode(500, new { message = "Update local guide error!", success = false });
            }
        }

        /// <summary>
        /// Unregister (delete) a local guide. A user may only delete their own local guide record.
        /// </summary>
        /// <param name="localGuideId"></param>
        /// <returns></returns>
        [HttpDelete("{localGuideId:guid}")]
        public async Task<IActionResult> DeleteLocalGuide(Guid localGuideId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to unregister a local guide.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                await _localGuidesRepository.DeleteLocalGuide(_supabase, localGuideId, userId, ct);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException err)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = err.Message, success = false });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error deleting local guide: {err.Message}");
                return StatusCode(500, new { message = "Delete local guide error!", success = false });
            }
        }

        private async Task<List<Guid>> ResolveCityIds(LocalGuideCityForm[]? cities)
        {
            var cityIds = new List<Guid>();
            if (cities == null)
                return cityIds;

            foreach (var city in cities)
            {
                if (string.IsNullOrEmpty(city.City))
                    continue;

                var resolved = await _cityRepository.InsertOrRetrieveCity(
                    _supabase, city.City, city.StateOrProvince, city.Country, city.Latitude, city.Longitude);
                cityIds.Add(resolved.Id);
            }

            return cityIds;
        }
    }
}
