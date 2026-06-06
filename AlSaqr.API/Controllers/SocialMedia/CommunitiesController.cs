using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class CommunitiesController : ControllerBase
    {

        private readonly ILogger<CommunitiesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ICommunityRepository _communityRepository;
        private readonly ICommunityMemberRepository _communityMemberRepository;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public CommunitiesController(
            ILogger<CommunitiesController> logger,
            Supabase.Client supabase,
            ICommunityRepository communityRepository,
            ICommunityMemberRepository communityMemberRepository,
            ISocialMediaCacheService socialMediaCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _communityRepository = communityRepository;
            _communityMemberRepository = communityMemberRepository;
            _socialMediaCacheService = socialMediaCacheService;
        }

        /// <summary>
        /// Returns communities
        /// </summary>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCommunities(
                Guid userId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10,
                [FromQuery] string? searchTerm = null
            )
        {

            if (_socialMediaCacheService.CheckIfInitialCommunitiesCanBeRetrieved(currentPage, userId))
                return Ok(_socialMediaCacheService.GetInitialCommunities(userId));

            var result = await _communityRepository.GetCommunities(_supabase, userId, searchTerm, currentPage, itemsPerPage);
            _socialMediaCacheService.SetInitialCommunities(result, userId);

            return Ok(result);
        }

        /// <summary>
        /// Returns community admin info
        /// </summary>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}")]
        public async Task<IActionResult> GetCommunity(Guid userId, Guid communityId)
        {
            // Input validation
            if (communityId == Guid.Empty)
            {
                return BadRequest("Community ID is required");
            }

            var community = await _communityRepository.GetCommunity(_supabase, userId, communityId);

            return Ok(community);
        }

        /// <summary>
        /// Create a community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CreateCommunity(
                [FromRoute] Guid userId,
                [FromBody] AlSaqrUpsertRequest<CreateCommunityFormDto> request)
        {
            var data = request.Values;
            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }

            if (string.IsNullOrEmpty(data?.Name))
            {
                return BadRequest("Name of Community is required");
            }

            var result = await _communityRepository.CreateCommunity(_supabase, userId, data);

            _socialMediaCacheService.ClearInitialCommunities(userId);

            return Ok(new { success = true });
        }

        /// <summary>
        /// Update a community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{userId}/{communityId}")]
        public async Task<IActionResult> UpdateCommunity(
            Guid userId,
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<UpdateCommunityForm> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required for updating your user.");
            }
            if (communityId == Guid.Empty)
            {
                return BadRequest("Community ID is required for updating your user.");
            }

            await _communityRepository.UpdateCommunity(_supabase, userId, communityId, data, ct);

            return Ok(new { succcess = true });
        }


        /// <summary>
        /// Delete a community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpDelete("{userId}/{communityId}")]
        public async Task<IActionResult> DeleteCommunity(
            Guid userId,
            Guid communityId)
        {

            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required for updating your user.");
            }
            if (communityId == Guid.Empty)
            {
                return BadRequest("Community ID is required for updating your user.");
            }

            await _communityRepository.DeleteCommunity(_supabase, userId, communityId);

            return Ok(new { succcess = true });
        }

        /// <summary>
        /// Join a public community.
        /// Migrated from Neo4j MERGE (user)-[:JOINED]->(community) + notification.
        /// </summary>
        [HttpPatch("{userId}/{communityId}/join")]
        public async Task<IActionResult> JoinCommunity(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId)
                || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(communityId, out var communityGuid))
                return BadRequest("User ID and Community ID must be valid GUIDs");

            try
            {
                await _communityMemberRepository.JoinCommunity(_supabase, userGuid, communityGuid, ct);
                return Ok(new { success = true, message = "Joined Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Join Community error!");
                return StatusCode(500, new { message = "Join Community error!", success = false });
            }
        }

        /// <summary>
        /// Unjoin a community.
        /// Migrated from Neo4j delete of INVITED/JOINED relationships + notification.
        /// </summary>
        [HttpPatch("{userId}/{communityId}/unjoin")]
        public async Task<IActionResult> UnJoinCommunity(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId)
                || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(communityId, out var communityGuid))
                return BadRequest("User ID and Community ID must be valid GUIDs");

            try
            {
                await _communityMemberRepository.UnJoinCommunity(_supabase, userGuid, communityGuid, ct);
                return Ok(new { success = true, message = "Left community Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Left Community error!");
                return StatusCode(500, new { message = "Left Community error!", success = false });
            }
        }

        /// <summary>
        /// Create a request to join a community.
        /// Migrated from Neo4j MERGE (community)-[:INVITE_REQUESTED]->(user) + notification.
        /// </summary>
        [HttpPost("{userId}/{communityId}/request-join")]
        public async Task<IActionResult> PostRequestJoin(
            Guid userId,
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (userId == Guid.Empty || communityId == Guid.Empty
                || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            if (userId == Guid.Empty || communityId == Guid.Empty)
                return BadRequest("User ID and Community ID must be valid GUIDs");

            try
            {
                await _communityMemberRepository.RequestJoinCommunity(_supabase, userId, communityId, ct);
                return Ok(new { success = true, message = "Request to join community successfully." });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Request to join community error!");
                return StatusCode(500, new { message = "Request to join community error!", success = false });
            }
        }

        /// <summary>
        /// Accept or deny a request to join a community.
        /// Migrated from Neo4j accept/deny MERGE/notification + INVITE_REQUESTED cleanup.
        /// </summary>
        [HttpPatch("{userId}/{communityId}/request-join")]
        public async Task<IActionResult> RequestJoin(
            Guid userId,
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<AcceptOrDenyCommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (userId == Guid.Empty || communityId == Guid.Empty)
                return BadRequest("Missing required fields.");

            try
            {
                await _communityMemberRepository.RespondToJoinRequest(
                    _supabase,
                    userId,
                    communityId,
                    accept: data.Accept == true,
                    ct
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Error accepting or deny request to join community!");
                return StatusCode(500, new { message = "Error accepting or deny request to join community!", success = false });
            }
        }
    }
}
