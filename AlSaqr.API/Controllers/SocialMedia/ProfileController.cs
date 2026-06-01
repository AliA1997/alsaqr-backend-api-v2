using Microsoft.AspNetCore.Mvc;
using AlSaqr.Data.Repositories.SocialMedia.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class ProfileController : ControllerBase
    {

        private readonly ILogger<ProfileController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IProfileRepository _profileRepository;


        public ProfileController(
            ILogger<ProfileController> logger, 
            Supabase.Client supabase,
            IProfileRepository profileRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _profileRepository = profileRepository;
        }

        /// <summary>
        /// Get user profile information based on username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>

        [HttpGet("{username}")]
        public async Task<IActionResult> GetProfileInfo(
            string username)
        {
            // Input validation
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }


            var userProfileInfo = await _profileRepository.GetProfileInfo(_supabase, username);

            if (userProfileInfo == null)
            {
                return NotFound(new { message = $"User Profile with a username of {username}", success = false });
            }

            return Ok(userProfileInfo);
        }


        /// <summary>
        /// Get user profile posts based on username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>

        [HttpGet("{username}/posts")]
        public async Task<IActionResult> GetProfilePosts(
            string username,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25)
        {
            // Input validation
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }

            var result = await _profileRepository.GetProfilePosts(_supabase, username, currentPage, itemsPerPage);

            return Ok(result);
        }
    }
}
