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
        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupDetails(Guid groupId)
        {
            var (groupDetails, events) = await _groupRepository.GetGroupDetails(_supabase, groupId);
   

            return Ok(new { events, groupDetails, success = true });
        }

        [HttpGet("{groupId}/similar")]
        public async Task<IActionResult> GetSimilarGroups(
            Guid groupId,
            [FromQuery] string latitude,
            [FromQuery] string longitude)
        {
            
            var result = await _groupRepository.GetSimilarGroups(
                _supabase,
                groupId,
                latitude,
                longitude);

            return Ok(result);
        }
    }
}
