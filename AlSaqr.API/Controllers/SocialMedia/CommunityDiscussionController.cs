using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
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


        public CommunityDiscussionController(
            ILogger<CommunityDiscussionController> logger,
            Supabase.Client supabase,
            ICommunityDiscussionRepository communityDiscussionRepository,
            ICommunityDiscussionMessageRepository communityDiscussionMessageRepository,
            ICommunityDiscussionMemberRepository communityDiscussionMemberRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _communityDiscussionRepository = communityDiscussionRepository;
            _communityDiscussionMessageRepository = communityDiscussionMessageRepository;
            _communityDiscussionMemberRepository = communityDiscussionMemberRepository;
        }

        /// <summary>
        /// Get all commmunity discussion given a community id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}")]
        public async Task<IActionResult> GetCommunityDiscussions(
            Guid userId,
            Guid communityId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {

            var result = await _communityDiscussionRepository.GetCommunityDiscussions(_supabase, userId, communityId, searchTerm, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Get community discussion based on community discussion id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}/{communityDiscussionId}")]
        public async Task<IActionResult> GetCommunityDiscussionInfo(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId)
        {
            if (communityId == Guid.Empty)
            {
                return BadRequest("Community Discussion must have an community id");
            }
                
            if (communityDiscussionId == Guid.Empty)
            {
                return BadRequest("Community Discussion must have an id");
            }

            var result = await _communityDiscussionRepository.GetCommunityDiscussion(_supabase, communityDiscussionId);

            return Ok(result);
        }

        /// <summary>
        /// Join a public community discussion
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/{communityDiscussionId}/join")]
        public async Task<IActionResult> JoinCommunity(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CommunityDiscussion.CommunityDiscussionInviteConfirmationDto> request)
        {
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
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/{communityDiscussionId}/unjoin")]
        public async Task<IActionResult> UnJoinCommunity(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CommunityDiscussion.CommunityDiscussionInviteConfirmationDto> request)
        {
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
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}/{communityId}")]
        public async Task<IActionResult> CreateCommunityDiscussion(
            [FromRoute] Guid userId,
            [FromRoute] Guid communityId,
            [FromBody] AlSaqrUpsertRequest<CreateCommunityDiscussionForm> request)
        {

            var data = request.Values;

            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }

            if (communityId == Guid.Empty)
            {
                return BadRequest("Community ID is required");
            }

            if (string.IsNullOrEmpty(data?.Name))
            {
                return BadRequest("Name of Community Discussion is required");
            }

            await _communityDiscussionRepository.CreateCommunityDiscussion(_supabase, userId, communityId, data);

            return Ok(new { success = true });
        }

        /// <summary>
        /// Create a request to join
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}/{communityId}/{communityDiscussionId}/request-join")]
        public async Task<IActionResult> PostRequestJoin(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<CommunityDiscussionInviteConfirmationDto> request)
        {
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
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/{communityDiscussionId}/request-join")]
        public async Task<IActionResult> RespondToRequestJoin(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<AcceptOrDenyCommunityDiscussionInviteConfirmationDto> request)
        {
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
        [HttpGet("{userId}/{communityId}/{communityDiscussionId}/admin")]
        public async Task<IActionResult> GetAdminCommunityDiscussionInfo(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId)
        {

            if (communityId == Guid.Empty)
            {
                return BadRequest("Community Discussion must have an community id");
            }

            if (communityDiscussionId == Guid.Empty)
            {
                return BadRequest("Community Discussion must have an id");
            }

            var result = _communityDiscussionRepository.GetAdminCommunityDiscussionInfo(_supabase, userId, communityDiscussionId);

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
        [HttpGet("{userId}/{communityId}/{communityDiscussionId}/messages")]
        public async Task<IActionResult> GetCommunityDiscussionMessages(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            if(communityId == Guid.Empty)
                return BadRequest("Community ID is required");

            if(communityDiscussionId == Guid.Empty)
                return BadRequest("Community Discussion ID is required");

            var result = await _communityDiscussionMessageRepository.GetCommunityDiscussionMessages(_supabase, communityDiscussionId, searchTerm, currentPage, itemsPerPage);

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
        [HttpPost("{userId}/{communityId}/{communityDiscussionId}/messages")]
        public async Task<IActionResult> CreateMessage(
            Guid userId,
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] CommunityDiscussion.CommunityDiscussionMessageDto request)
        {
            if (userId == Guid.Empty || communityId == Guid.Empty || communityDiscussionId == Guid.Empty || string.IsNullOrEmpty(request.MessageText))
            {
                return BadRequest("Missing required fields");
            }

            //await _communityDiscussionMessageRepository.Create

            return Ok(new { success = true, message = "Sent community discussion message successfully!:)" });
        }

    }

}
