using Microsoft.AspNetCore.Mvc;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Data.Repositories.Zook.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class ProfileController : ControllerBase
    {

        private readonly ILogger<ProfileController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IProfileRepository _profileRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICommunityRepository _communityRepository;
        private readonly ICommunityDiscussionRepository _communityDiscussionRepository;


        public ProfileController(
            ILogger<ProfileController> logger, 
            Supabase.Client supabase,
            IProfileRepository profileRepository,
            IEventRepository eventRepository,
            IGroupRepository groupRepository,
            IProductRepository productRepository,
            ICommunityRepository communityRepository,
            ICommunityDiscussionRepository communityDiscussionRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _profileRepository = profileRepository;
            _eventRepository = eventRepository;
            _groupRepository = groupRepository;
            _productRepository = productRepository;
            _communityRepository = communityRepository;
            _communityDiscussionRepository = communityDiscussionRepository;
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

        /// <summary>
        /// Get user profile posts with media
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>

        [HttpGet("{username}/media-posts")]
        public async Task<IActionResult> GetProfileMediaPosts(
            string username,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25)
        {
            // Input validation
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }

            var result = await _profileRepository.GetProfileMediaPosts(_supabase, username, currentPage, itemsPerPage);

            return Ok(result);
        }
        
        
        /// <summary>
        /// Get the events a given user (by username) attended, paginated and ordered
        /// earliest-attendance first, then event name descending (spec EA-1..EA-5).
        /// </summary>
        /// <param name="username"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{username}/events")]
        public async Task<IActionResult> GetAttendedEvents(
                string username,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null
            )
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required");

            var result = await _eventRepository.GetAttendedEvents(
                _supabase,
                username,
                currentPage,
                itemsPerPage,
                searchTerm);

            return Ok(result);
        }



        /// <summary>
        /// Get the groups a given user (by username) has joined, paginated and
        /// ordered most-recently-joined first (spec GJ-1..GJ-5).
        /// </summary>
        /// <param name="username"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{username}/groups")]
        public async Task<IActionResult> GetJoinedGroups(
                string username,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null
            )
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required");


            var result = await _groupRepository.GetJoinedGroups(
                _supabase,
                username,
                currentPage,
                itemsPerPage,
                searchTerm);

            return Ok(result);
        }


        /// <summary>
        /// Get the products belonging to a given user (by username), paginated and
        /// ordered most-recently-created first, optionally filtered by product name
        /// (spec PROD-1..PROD-6).
        /// </summary>
        /// <param name="username"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{username}/products")]
        public async Task<IActionResult> GetUserProducts(
                string username,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null
            )
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required");

            var result = await _productRepository.GetUserProducts(
                _supabase,
                username,
                currentPage,
                itemsPerPage,
                searchTerm);

            return Ok(result);
        }

        /// <summary>
        /// Get the communities belonging to a given user (by username), paginated and
        /// ordered most-recently-created first, optionally filtered by product name
        /// (spec PROD-1..PROD-6).
        /// </summary>
        /// <param name="username"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{username}/communities")]
        public async Task<IActionResult> GetUserCommunities(
                string username,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null
            )
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required");

            var result = await _communityRepository.GetUserCommunities(
                _supabase,
                username,
                currentPage,
                itemsPerPage,
                searchTerm);

            return Ok(result);
        }

        /// <summary>
        /// Get the community discussions belonging to a given user (by username), paginated and
        /// ordered most-recently-created first, optionally filtered by product name
        /// (spec PROD-1..PROD-6).
        /// </summary>
        /// <param name="username"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{username}/communityDiscussions")]
        public async Task<IActionResult> GetUserCommunityDiscussions(
                string username,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null
            )
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required");

            var result = await _communityDiscussionRepository.GetUserCommunityDiscussions(
                _supabase,
                username,
                currentPage,
                itemsPerPage,
                searchTerm);

            return Ok(result);
        }
    }
}
