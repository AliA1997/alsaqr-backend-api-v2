using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class CommunityDiscussionController : ControllerBase
    {

        private readonly ILogger<CommunityDiscussionController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ICommunityDiscussionRepository _communityDiscussionRepository;
        private readonly ICommunityDiscussionMessageRepository _communityDiscussionMessageRepository;
        private readonly ICommunityDiscussionMemberRepository _communityDiscussionMemberRepository;
        private readonly ISocialMediaCacheService _socialMediaCacheService;
        private readonly IUserCacheService _userCacheService;

        public CommunityDiscussionController(
            ILogger<CommunityDiscussionController> logger,
            Supabase.Client supabase,
            ICommunityDiscussionRepository communityDiscussionRepository,
            ICommunityDiscussionMessageRepository communityDiscussionMessageRepository,
            ICommunityDiscussionMemberRepository communityDiscussionMemberRepository,
            ISocialMediaCacheService socialMediaCacheService,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _communityDiscussionRepository = communityDiscussionRepository;
            _communityDiscussionMessageRepository = communityDiscussionMessageRepository;
            _communityDiscussionMemberRepository = communityDiscussionMemberRepository;
            _socialMediaCacheService = socialMediaCacheService;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Get all commmunity discussion given a community id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{communityId}")]
        public async Task<IActionResult> GetCommunityDiscussions(
            Guid communityId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to retrieve community discussions");
            if (communityId == Guid.Empty)
                return BadRequest("Community ID is required to retrieve community discussions");
            Guid.TryParse(loggedInUser.Id?.ToString(), out Guid userId);

            if (_socialMediaCacheService.CheckIfInitialCommunityDiscussionsCanBeRetrieved(userId, communityId))
                return Ok(_socialMediaCacheService.GetInitialCommunityDiscussions(userId, communityId));

            var result = await _communityDiscussionRepository.GetCommunityDiscussions(_supabase, userId, communityId, searchTerm, currentPage, itemsPerPage);

            _socialMediaCacheService.SetInitialCommunityDiscussions(result, userId, communityId);

            return Ok(result);
        }

        /// <summary>
        /// Get community discussion based on community discussion id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <returns></returns>
        [HttpGet("{communityId}/{communityDiscussionId}")]
        public async Task<IActionResult> GetCommunityDiscussionInfo(
            Guid communityId,
            Guid communityDiscussionId)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to retrieve community discussions");
            if (communityId == Guid.Empty)
                return BadRequest("Community Discussion must have an community id");   
            if (communityDiscussionId == Guid.Empty)
                return BadRequest("Community Discussion must have an id");
            Guid.TryParse(loggedInUser.Id?.ToString(), out Guid userId);

            var result = await _communityDiscussionRepository.GetCommunityDiscussion(_supabase, userId, communityDiscussionId);

            return Ok(result);
        }

        /// <summary>
        /// Join a public community discussion
        /// </summary>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{communityId}/{communityDiscussionId}/join")]
        public async Task<IActionResult> JoinCommunityDiscussion(
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CommunityDiscussionInviteConfirmationDto> request)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to join a community discussion");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);
            
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;
            if (userId == Guid.Empty || communityId == Guid.Empty || communityDiscussionId == Guid.Empty
                || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            var result = _communityDiscussionMemberRepository.JoinCommunityDiscussion(_supabase, userId, communityDiscussionId, ct);


            return Ok(new { success = true, message = "Joined Discussion Successfully" });

        }

        /// <summary>
        /// Unjoin a community discussion
        /// </summary>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{communityId}/{communityDiscussionId}/unjoin")]
        public async Task<IActionResult> UnJoinCommunityDiscussion(
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CommunityDiscussionInviteConfirmationDto> request)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged unjoin from a community discussion.");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;
            if (userId == Guid.Empty || communityId == Guid.Empty || communityDiscussionId == Guid.Empty
                || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }
            
            var result = _communityDiscussionMemberRepository.UnJoinCommunityDiscussion(_supabase, userId, communityDiscussionId, ct);

            return Ok(new { success = true, message = "Left community discussion Successfully" });

        }

        /// <summary>
        /// Create a community discussion
        /// </summary>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{communityId}")]
        public async Task<IActionResult> CreateCommunityDiscussion(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<CreateCommunityDiscussionForm> request)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to retrieve community discussions");
            if (communityId == Guid.Empty)
                return BadRequest("Community ID is required");
            Guid.TryParse(loggedInUser.Id?.ToString(), out Guid userId);

            var data = request.Values;
            if (string.IsNullOrEmpty(data?.Name))
                return BadRequest("Name of Community Discussion is required");



            await _communityDiscussionRepository.CreateCommunityDiscussion(_supabase, userId, communityId, data);
            _socialMediaCacheService.ClearInitialCommunityDiscussions(userId, communityId);

            return Ok(new { success = true });
        }

        /// <summary>
        /// Create a request to join
        /// </summary>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{communityId}/{communityDiscussionId}/request-join")]
        public async Task<IActionResult> PostRequestJoinCommunityDiscussion(
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CommunityDiscussionInviteConfirmationDto> request)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to request to join a community discussion.");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;
            if (userId == Guid.Empty || communityId == Guid.Empty || communityDiscussionId == Guid.Empty
                || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            await _communityDiscussionMemberRepository.RequestJoinCommunityDiscussion(_supabase, userId, communityDiscussionId, ct);

            return Ok(new { success = true, message = "Request to join community discussion successfully." });

        }


        /// <summary>
        /// Accept or deny a request to join
        /// </summary>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{communityId}/{communityDiscussionId}/request-join")]
        public async Task<IActionResult> RespondToRequestJoinCommunityDiscussion(
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<AcceptOrDenyCommunityDiscussionInviteConfirmationDto> request)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to respond to a join request to a community discussion.");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            if (communityId == Guid.Empty)
                return BadRequest("Community ID is required");

            if (communityDiscussionId == Guid.Empty)
                return BadRequest("Community Discussion ID is required");

            await _communityDiscussionMemberRepository.RespondToJoinRequest(_supabase, userId, communityDiscussionId, request.Values.Accept, ct);

            return Ok(new { success = true });

        }

        /// <summary>
        /// Get admin community discussion based on community discussion id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <returns></returns>
        [HttpGet("{communityId}/{communityDiscussionId}/admin")]
        public async Task<IActionResult> GetAdminCommunityDiscussionInfo(
            Guid communityId,
            Guid communityDiscussionId)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to retrieve community discussions");
            if (communityId == Guid.Empty)
                return BadRequest("Community Discussion must have an community id");
            if (communityDiscussionId == Guid.Empty)
                return BadRequest("Community Discussion must have an id");
            Guid.TryParse(loggedInUser.Id?.ToString(), out Guid userId);

            var result = await _communityDiscussionRepository.GetAdminCommunityDiscussionInfo(_supabase, userId, communityDiscussionId);

            return Ok(result);
        }

        /// <summary>
        /// Get messages for a given community discussion
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{communityId}/{communityDiscussionId}/messages")]
        public async Task<IActionResult> GetCommunityDiscussionMessages(
            Guid communityId,
            Guid communityDiscussionId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to retrieve community discussion messages.");
            if (communityId == Guid.Empty)
                return BadRequest("Community ID is required");
            if(communityDiscussionId == Guid.Empty)
                return BadRequest("Community Discussion ID is required");
            Guid.TryParse(loggedInUser.Id?.ToString(), out Guid userId);

            if (_socialMediaCacheService.CheckIfInitialCommunityDiscussionMessagesCanBeRetrieved(userId, communityId, communityDiscussionId))
                return Ok(_socialMediaCacheService.GetInitialCommunityDiscussionMessages(userId, communityId, communityDiscussionId));

            var result = await _communityDiscussionMessageRepository.GetCommunityDiscussionMessages(_supabase, communityDiscussionId, searchTerm, currentPage, itemsPerPage);

            _socialMediaCacheService.SetInitialCommunityDiscussionMessages(result, userId, communityId, communityDiscussionId);
            return Ok(result);

        }


        /// <summary>
        /// Create a request to join
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{communityId}/{communityDiscussionId}/messages")]
        public async Task<IActionResult> CreateMessage(
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CreateCommunityDiscussionMessageForm> request)
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to create a community discussion message.");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            var data = request.Values;

            if (communityId == Guid.Empty || communityDiscussionId == Guid.Empty || string.IsNullOrEmpty(data.Content))
                return BadRequest("Missing required fields");

            await _communityDiscussionRepository.CreateCommunityDiscussionMessage(_supabase, userId, communityDiscussionId, data);

            _socialMediaCacheService.ClearInitialCommunityDiscussionMessages(userId, communityId, communityDiscussionId);

            return Ok(new { success = true, message = "Sent community discussion message successfully!:)" });
        }

    }

}
