using AlSaqr.Data.Repositories.Meetup.Impl;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class CitiesController : ControllerBase
    {
        private readonly ILogger<CitiesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ICityRepository _cityRepository;

        public CitiesController(
            ILogger<CitiesController> logger,
            Supabase.Client supabase,
            ICityRepository cityRepository
        )
        {
            _logger = logger;
            _supabase = supabase;
            _cityRepository = cityRepository;
        }

        /// <summary>
        /// Get up to 100 distinct cities for a dropdown. Not paginated — this is global
        /// reference data, so it is not user-scoped and is returned as a flat list.
        /// </summary>
        /// <returns>A list of <see cref="AlSaqr.Domain.Meetup.CityDto"/>.</returns>
        [HttpGet]
        public async Task<IActionResult> GetCities([FromQuery] string searchTerm)
        {
            var result = await _cityRepository.GetCities(_supabase, searchTerm);
            return Ok(result);
        }

        /// <summary>
        /// For populating the update event modal
        /// </summary>
        /// <returns>A list of <see cref="AlSaqr.Domain.Meetup.CityDto"/>.</returns>
        [HttpGet("{cityId}")]
        public async Task<IActionResult> GetCitiesByName(Guid cityId)
        {
            var result = await _cityRepository.GetCitiesById(_supabase, cityId);
            return Ok(result);
        }
    }
}
