using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Spaces;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.SocialMedia.Spaces;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    /// <summary>
    /// Ephemeral audio spaces (specs/audio-spaces.md). Routes, body envelopes and
    /// response shapes are a frozen contract with the React client. Every SFU
    /// interaction is proxied here — the Cloudflare app secret never reaches the
    /// browser. Nothing on this controller is cached: spaces are real-time state
    /// and are always read live (§4.1 exemption). Authorization outcomes
    /// (403/404/409) surface via repository exceptions mapped by the global
    /// exception middleware.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class SpacesController : AuthorizedControllerBase
    {
        private readonly ILogger<SpacesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ISpaceRepository _spaceRepository;
        private readonly ICloudflareCallsService _cloudflareCalls;
        private readonly ISpaceEventBroadcaster _broadcaster;
        private readonly IUserCacheService _userCacheService;

        public SpacesController(
            ILogger<SpacesController> logger,
            Supabase.Client supabase,
            ISpaceRepository spaceRepository,
            ICloudflareCallsService cloudflareCalls,
            ISpaceEventBroadcaster broadcaster,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _spaceRepository = spaceRepository;
            _cloudflareCalls = cloudflareCalls;
            _broadcaster = broadcaster;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// The live space for a community, or null when none is live.
        /// </summary>
        [HttpGet("community/{communityId}/live")]
        public async Task<IActionResult> GetLiveCommunitySpace(Guid communityId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;
            if (communityId == Guid.Empty)
                return BadRequest("Community ID is required");

            var result = await _spaceRepository.GetLiveCommunitySpace(
                _supabase, communityId, HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// The live space for a community discussion, or null when none is live.
        /// </summary>
        [HttpGet("community/{communityId}/discussion/{communityDiscussionId}/live")]
        public async Task<IActionResult> GetLiveCommunityDiscussionSpace(
            Guid communityId,
            Guid communityDiscussionId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;
            if (communityId == Guid.Empty || communityDiscussionId == Guid.Empty)
                return BadRequest("Community ID and Community Discussion ID are required");

            var result = await _spaceRepository.GetLiveCommunityDiscussionSpace(
                _supabase, communityId, communityDiscussionId, HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// Starts a community space; the caller becomes host.
        /// </summary>
        [HttpPost("community/{communityId}")]
        public async Task<IActionResult> StartCommunitySpace(
            Guid communityId,
            [FromBody] AlSaqrUpsertRequest<StartSpaceForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            if (userId == Guid.Empty || communityId == Guid.Empty)
                return BadRequest("Missing required fields");
            if (string.IsNullOrWhiteSpace(request.Values?.Title))
                return BadRequest("A title is required to start a space");

            var result = await _spaceRepository.StartCommunitySpace(
                _supabase, userId, communityId, request.Values.Title, HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// Starts a community-discussion space; the caller becomes host.
        /// </summary>
        [HttpPost("community/{communityId}/discussion/{communityDiscussionId}")]
        public async Task<IActionResult> StartCommunityDiscussionSpace(
            Guid communityId,
            Guid communityDiscussionId,
            [FromBody] AlSaqrUpsertRequest<StartSpaceForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            if (userId == Guid.Empty || communityId == Guid.Empty || communityDiscussionId == Guid.Empty)
                return BadRequest("Missing required fields");
            if (string.IsNullOrWhiteSpace(request.Values?.Title))
                return BadRequest("A title is required to start a space");

            var result = await _spaceRepository.StartCommunityDiscussionSpace(
                _supabase, userId, communityId, communityDiscussionId, request.Values.Title,
                HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// Joins a live space. Returns the space, the caller's role, the roster,
        /// and the currently publishing speakers so the joiner can pull each track.
        /// </summary>
        [HttpPost("{spaceId}/join")]
        public async Task<IActionResult> JoinSpace(Guid spaceId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            if (userId == Guid.Empty || spaceId == Guid.Empty)
                return BadRequest("Missing required fields");

            var result = await _spaceRepository.JoinSpace(
                _supabase, userId, spaceId, HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        /// Leaves a space: closes the caller's published track (if any) on the SFU
        /// and announces track_closed. Idempotent — every client exit path calls it.
        /// </summary>
        [HttpPost("{spaceId}/leave")]
        public async Task<IActionResult> LeaveSpace(Guid spaceId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            if (userId == Guid.Empty || spaceId == Guid.Empty)
                return BadRequest("Missing required fields");

            var ct = HttpContext.RequestAborted;
            var participant = await _spaceRepository.LeaveSpace(_supabase, userId, spaceId, ct);

            if (participant != null)
            {
                if (!string.IsNullOrEmpty(participant.SfuSessionId) && !string.IsNullOrEmpty(participant.SfuMid))
                    await _cloudflareCalls.CloseTrackAsync(participant.SfuSessionId, participant.SfuMid, ct);

                await _broadcaster.TrackClosedAsync(spaceId, userId, ct);
            }

            return NoContent();
        }

        /// <summary>
        /// Ends a space (host only): closes every published track and announces
        /// space_ended with the recorded ended_at.
        /// </summary>
        [HttpPost("{spaceId}/end")]
        public async Task<IActionResult> EndSpace(Guid spaceId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            if (userId == Guid.Empty || spaceId == Guid.Empty)
                return BadRequest("Missing required fields");

            var ct = HttpContext.RequestAborted;
            var (space, publishers) = await _spaceRepository.EndSpace(_supabase, userId, spaceId, ct);

            foreach (var publisher in publishers)
            {
                if (!string.IsNullOrEmpty(publisher.SfuSessionId) && !string.IsNullOrEmpty(publisher.SfuMid))
                    await _cloudflareCalls.CloseTrackAsync(publisher.SfuSessionId, publisher.SfuMid, ct);
            }

            await _broadcaster.SpaceEndedAsync(space.Id, space.EndedAt!.Value, ct);

            return NoContent();
        }

        /// <summary>
        /// Publishes the caller's audio: the client OFFERS, the SFU ANSWERS. The
        /// persisted role must be host or speaker at the time of this call — a
        /// listener gets 403 before any SFU interaction happens.
        /// </summary>
        [HttpPost("{spaceId}/publish")]
        public async Task<IActionResult> Publish(
            Guid spaceId,
            [FromBody] AlSaqrUpsertRequest<PublishForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            var data = request.Values;
            if (userId == Guid.Empty || spaceId == Guid.Empty
                || string.IsNullOrEmpty(data?.Sdp) || string.IsNullOrEmpty(data.Mid))
            {
                return BadRequest("Missing required fields");
            }

            var ct = HttpContext.RequestAborted;

            // Role gate first — the SFU is never touched for an unauthorized caller.
            var participant = await _spaceRepository.GetPublisherParticipant(_supabase, spaceId, userId, ct);

            // A re-publish (e.g. after an ICE restart) replaces the old track.
            if (!string.IsNullOrEmpty(participant.SfuSessionId) && !string.IsNullOrEmpty(participant.SfuMid))
                await _cloudflareCalls.CloseTrackAsync(participant.SfuSessionId, participant.SfuMid, ct);

            var published = await _cloudflareCalls.PublishAsync(data.Sdp, data.Mid, ct);

            await _spaceRepository.SetPublishState(
                _supabase, spaceId, userId, published.SessionId, published.TrackName, data.Mid, ct);

            await _broadcaster.TrackAddedAsync(spaceId, userId, published.SessionId, published.TrackName, ct);

            return Ok(new PublishResultDto
            {
                SessionId = published.SessionId,
                TrackName = published.TrackName,
                Answer = new SessionDescriptionDto { Type = "answer", Sdp = published.AnswerSdp },
            });
        }

        /// <summary>
        /// Pulls one speaker's track for the caller: the SFU OFFERS and the client
        /// answers via renegotiate. Available to any participant of the space; each
        /// call is scoped to a single track so other subscriptions are undisturbed.
        /// </summary>
        [HttpPost("{spaceId}/subscribe")]
        public async Task<IActionResult> Subscribe(
            Guid spaceId,
            [FromBody] AlSaqrUpsertRequest<SubscribeForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            var data = request.Values;
            if (userId == Guid.Empty || spaceId == Guid.Empty
                || string.IsNullOrEmpty(data?.PubSessionId) || string.IsNullOrEmpty(data.TrackName))
            {
                return BadRequest("Missing required fields");
            }

            var ct = HttpContext.RequestAborted;

            await _spaceRepository.GetLiveParticipant(_supabase, spaceId, userId, ct);

            var subscribed = await _cloudflareCalls.SubscribeAsync(
                existingSessionId: null, data.PubSessionId, data.TrackName, ct);

            return Ok(new SubscribeResultDto
            {
                SessionId = subscribed.SessionId,
                Offer = new SessionDescriptionDto { Type = "offer", Sdp = subscribed.OfferSdp },
            });
        }

        /// <summary>
        /// Forwards the client's SDP answer for a pending subscribe renegotiation.
        /// Ordering is strict and owned by the client; the backend forwards in order.
        /// </summary>
        [HttpPost("{spaceId}/renegotiate")]
        public async Task<IActionResult> Renegotiate(
            Guid spaceId,
            [FromBody] AlSaqrUpsertRequest<RenegotiateForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            var data = request.Values;
            if (userId == Guid.Empty || spaceId == Guid.Empty
                || string.IsNullOrEmpty(data?.SessionId) || string.IsNullOrEmpty(data.Sdp))
            {
                return BadRequest("Missing required fields");
            }

            var ct = HttpContext.RequestAborted;

            await _spaceRepository.GetLiveParticipant(_supabase, spaceId, userId, ct);
            await _cloudflareCalls.RenegotiateAsync(data.SessionId, data.Sdp, ct);

            return NoContent();
        }

        /// <summary>
        /// Sets the caller's raised-hand flag. The hand_raised realtime event itself
        /// is client-emitted; the backend only persists the authoritative state.
        /// </summary>
        [HttpPost("{spaceId}/raise-hand")]
        public async Task<IActionResult> RaiseHand(
            Guid spaceId,
            [FromBody] AlSaqrUpsertRequest<RaiseHandForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var userId = GetLoggedInUserId();
            if (userId == Guid.Empty || spaceId == Guid.Empty || request.Values == null)
                return BadRequest("Missing required fields");

            await _spaceRepository.SetHandRaised(
                _supabase, spaceId, userId, request.Values.Raised, HttpContext.RequestAborted);

            return NoContent();
        }

        /// <summary>
        /// Approves a raised hand (host only): listener → speaker. The promoted
        /// client runs the publish step when it receives role_changed.
        /// </summary>
        [HttpPost("{spaceId}/speakers/{userId}/approve")]
        public async Task<IActionResult> ApproveSpeaker(Guid spaceId, Guid userId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var hostUserId = GetLoggedInUserId();
            if (hostUserId == Guid.Empty || spaceId == Guid.Empty || userId == Guid.Empty)
                return BadRequest("Missing required fields");

            var ct = HttpContext.RequestAborted;
            var target = await _spaceRepository.ApproveSpeaker(_supabase, hostUserId, spaceId, userId, ct);

            await _broadcaster.RoleChangedAsync(spaceId, target.UserId, target.Role, ct);

            return NoContent();
        }

        /// <summary>
        /// Demotes a speaker (host only): speaker → listener. The published track is
        /// closed on the SFU — demotion, not mute, is what unpublishes.
        /// </summary>
        [HttpPost("{spaceId}/speakers/{userId}/demote")]
        public async Task<IActionResult> DemoteSpeaker(Guid spaceId, Guid userId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var hostUserId = GetLoggedInUserId();
            if (hostUserId == Guid.Empty || spaceId == Guid.Empty || userId == Guid.Empty)
                return BadRequest("Missing required fields");

            var ct = HttpContext.RequestAborted;
            var demoted = await _spaceRepository.DemoteSpeaker(_supabase, hostUserId, spaceId, userId, ct);

            if (!string.IsNullOrEmpty(demoted.SfuSessionId) && !string.IsNullOrEmpty(demoted.SfuMid))
            {
                await _cloudflareCalls.CloseTrackAsync(demoted.SfuSessionId, demoted.SfuMid, ct);
                await _broadcaster.TrackClosedAsync(spaceId, demoted.UserId, ct);
            }

            await _broadcaster.RoleChangedAsync(spaceId, demoted.UserId, SpaceParticipant.RoleListener, ct);

            return NoContent();
        }

        private Guid GetLoggedInUserId()
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);
            return userId;
        }
    }
}
