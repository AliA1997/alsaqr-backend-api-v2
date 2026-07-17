using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class GroupDetailsController : ControllerBase 
    {
        private readonly ILogger<GroupDetailsController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly IGroupRepository _groupRepository;

        public GroupDetailsController(
            ILogger<GroupDetailsController> logger,
            Supabase.Client supabase,
            IGroupRepository groupRepository,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _groupRepository = groupRepository;
            _userCacheService = userCacheService;
        }


        /// <summary>
        /// Get group details
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpGet("{groupSlug}")]
        public async Task<IActionResult> GetGroupDetails(string groupSlug)
        {
            // Anonymous callers have no member to exclude, so an empty id excludes no one.
            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            var (groupDetails, events) = await _groupRepository.GetGroupDetails(_supabase, groupSlug, userId);
   

            return Ok(new { events, groupDetails, success = true });
        }

        /// <summary>
        /// Get the members belonging to a group
        /// </summary>
        /// <param name="groupSlug"></param>
        /// <returns></returns>
        [HttpGet("{groupSlug}/members")]
        public async Task<IActionResult> GetGroupMembers(
            string groupSlug,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            // Anonymous callers have no member to exclude, so an empty id excludes no one.
            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            var result = await _groupRepository.GetGroupMembers(
                _supabase,
                groupSlug: groupSlug,
                userId: userId,
                currentPage: currentPage,
                itemsPerPage: itemsPerPage,
                searchTerm: searchTerm);

            var groupMembers = result.Items;
            var pagination = result.Pagination;

            return Ok(new { groupMembers, pagination, success = true });
        }

        [HttpGet("{groupSlug}/similar")]
        public async Task<IActionResult> GetSimilarGroups(
            string groupSlug,
            [FromQuery] string latitude,
            [FromQuery] string longitude)
        {
            
            var result = await _groupRepository.GetSimilarGroups(
                _supabase,
                groupSlug,
                latitude,
                longitude);

            return Ok(result);
        }
    }
}
