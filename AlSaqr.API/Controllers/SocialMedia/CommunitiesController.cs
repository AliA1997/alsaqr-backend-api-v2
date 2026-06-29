using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.SocialMedia.Community;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class CommunitiesController : AuthorizedControllerBase
    {
        private readonly ILogger<CommunitiesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ICommunityRepository _communityRepository;
        private readonly ICommunityMemberRepository _communityMemberRepository;
        private readonly ISocialMediaCacheService _socialMediaCacheService;
        private readonly IUserCacheService _userCacheService;

        public CommunitiesController(
            ILogger<CommunitiesController> logger,
            Supabase.Client supabase,
            ICommunityRepository communityRepository,
            ICommunityMemberRepository communityMemberRepository,
            ISocialMediaCacheService socialMediaCacheService,
            IUserCacheService userCacheService
        )
        {
            _logger = logger;
            _supabase = supabase;
            _communityRepository = communityRepository;
            _communityMemberRepository = communityMemberRepository;
            _socialMediaCacheService = socialMediaCacheService;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Returns communities
        /// </summary>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetCommunities(
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("User must be logged in to create list.");

            Guid.TryParse(user.Id?.ToString(), out Guid userId);
            var noSearchTerm = string.IsNullOrEmpty(searchTerm ?? "".Trim());
            if (
                noSearchTerm
                && _socialMediaCacheService.CheckIfInitialCommunitiesCanBeRetrieved(userId)
            )
                return Ok(_socialMediaCacheService.GetInitialCommunities(userId));

            var result = await _communityRepository.GetCommunities(
                _supabase,
                userId,
                searchTerm,
                currentPage,
                itemsPerPage
            );
            if (noSearchTerm)
                _socialMediaCacheService.SetInitialCommunities(result, userId);

            return Ok(result);
        }

        /// <summary>
        /// Returns community admin info
        /// </summary>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{communityId}/admin")]
        public async Task<IActionResult> GetAdminCommunity(Guid communityId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("User must be logged in to view community details.");

            Guid.TryParse(user.Id?.ToString(), out Guid userId);
            // Input validation
            if (communityId == Guid.Empty)
            {
                return BadRequest(
                    "Community ID is required for retrieving admin community information"
                );
            }

            var community = await _communityRepository.GetAdminCommunityInfo(
                _supabase,
                userId,
                communityId
            );

            return Ok(community);
        }

        /// <summary>
        /// Returns community admin info
        /// </summary>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{communityId}")]
        public async Task<IActionResult> GetCommunity(Guid communityId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("User must be logged in to view community details.");

            Guid.TryParse(user.Id?.ToString(), out Guid userId);

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
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateCommunity(
            [FromBody] AlSaqrUpsertRequest<CreateCommunityFormDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);
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
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{communityId}")]
        public async Task<IActionResult> UpdateCommunity(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<UpdateCommunityForm> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);

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
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpDelete("{communityId}")]
        public async Task<IActionResult> DeleteCommunity(Guid communityId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);

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
        [HttpPatch("{communityId}/join")]
        public async Task<IActionResult> JoinCommunity(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);

            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (
                userId == Guid.Empty
                || communityId == Guid.Empty
                || string.IsNullOrEmpty(data.Email)
                || string.IsNullOrEmpty(data.Username)
            )
            {
                return BadRequest("Missing required fields");
            }

            try
            {
                await _communityMemberRepository.JoinCommunity(_supabase, userId, communityId, ct);
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
        /// </summary>
        [HttpPatch("{communityId}/unjoin")]
        public async Task<IActionResult> UnJoinCommunity(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);

            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            if (
                userId == Guid.Empty
                || communityId == Guid.Empty
                || string.IsNullOrEmpty(data.Email)
                || string.IsNullOrEmpty(data.Username)
            )
            {
                return BadRequest("Missing required fields");
            }

            try
            {
                await _communityMemberRepository.UnJoinCommunity(
                    _supabase,
                    userId,
                    communityId,
                    ct
                );
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
        [HttpPost("{communityId}/request-join")]
        public async Task<IActionResult> PostRequestJoin(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);

            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (
                userId == Guid.Empty
                || communityId == Guid.Empty
                || string.IsNullOrEmpty(data.Email)
                || string.IsNullOrEmpty(data.Username)
            )
            {
                return BadRequest("Missing required fields");
            }

            if (userId == Guid.Empty || communityId == Guid.Empty)
                return BadRequest("User ID and Community ID must be valid GUIDs");

            try
            {
                await _communityMemberRepository.RequestJoinCommunity(
                    _supabase,
                    userId,
                    communityId,
                    ct
                );
                return Ok(
                    new { success = true, message = "Request to join community successfully." }
                );
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Request to join community error!");
                return StatusCode(
                    500,
                    new { message = "Request to join community error!", success = false }
                );
            }
        }

        /// <summary>
        /// Accept or deny a request to join a community.
        /// Migrated from Neo4j accept/deny MERGE/notification + INVITE_REQUESTED cleanup.
        /// </summary>
        [HttpPatch("{communityId}/request-join")]
        public async Task<IActionResult> RequestJoin(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<AcceptOrDenyCommunityInviteConfirmationDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var user = _userCacheService.GetLoggedInUser();
            Guid.TryParse(user?.Id?.ToString(), out Guid userId);

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
                return StatusCode(
                    500,
                    new
                    {
                        message = "Error accepting or deny request to join community!",
                        success = false,
                    }
                );
            }
        }
    }
}
