using AlSaqr.API.HostedServices;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static AlSaqr.Domain.Meetup.EventVideo;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.Meetup
{
    /// <summary>
    /// Live video for online events (specs/video-streaming-online-events.md §8). Every
    /// route is event-scoped: one stream per event is live. Authorization is decided
    /// by the repository before any SFU call or token mint; failures surface as
    /// repository exceptions mapped by ExceptionHandlingMiddleware. Responses are bare
    /// DTOs (spec §8 envelope decision), never entities. Nothing here is cached: it is
    /// real-time state, and join tokens are per caller.
    /// </summary>
    /// <remarks>
    /// Ordering. Each action orders its SFU and database steps so that a failure
    /// leaves a state a retry (or the reaper) completes:
    /// <list type="bullet">
    /// <item>Leave and end act on the SFU first, as the reaper does. Their database
    /// step cannot be repeated (a second leave or end finds nothing live), so it runs
    /// only after the SFU step succeeded.</item>
    /// <item>Start, join, promote and demote write the database first. The
    /// authorization and the room name come from that write, a retry repeats it
    /// idempotently and re-applies the SFU step, and a role change must be in the
    /// database before any token is minted from it.</item>
    /// </list>
    /// Once a database write has committed, the remaining steps ignore the request's
    /// abort signal, so a client that disconnects cannot stop an action half-way.
    /// Broadcasts are notifications sent after everything else succeeded: a failed
    /// broadcast is logged and never turns a completed action into an error.
    /// </remarks>
    [ApiController]
    [Route("[controller]")]
    public class EventVideoController : AuthorizedControllerBase
    {
        private readonly Supabase.Client _supabase;
        private readonly IVideoStreamRepository _videoStreams;
        private readonly IVideoSfuService _sfu;
        private readonly IEventVideoBroadcaster _broadcaster;
        private readonly IAccessTokenVerifier _tokens;
        private readonly GaleneConfig _config;
        private readonly ILogger<EventVideoController> _logger;

        public EventVideoController(
            Supabase.Client supabase,
            IVideoStreamRepository videoStreams,
            IVideoSfuService sfu,
            IEventVideoBroadcaster broadcaster,
            IAccessTokenVerifier tokens,
            IOptions<GaleneConfig> config,
            ILogger<EventVideoController> logger)
        {
            _supabase = supabase;
            _videoStreams = videoStreams;
            _sfu = sfu;
            _broadcaster = broadcaster;
            _tokens = tokens;
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// 200 with the live stream, or 204 when none is live. Only attendees and those
        /// who may control the event's video can see it.
        /// </summary>
        [HttpGet("event/{eventId}/live")]
        public async Task<IActionResult> GetLiveStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            return Ok(await _videoStreams.GetLiveEventStreamForUser(
                _supabase, callerId, eventId, HttpContext.RequestAborted));
        }

        [HttpPost("event/{eventId}/start")]
        public async Task<IActionResult> StartStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            // One repository call writes the stream and its host row (and undoes the
            // stream if the host row fails), so start stays atomic from here.
            var stream = await _videoStreams.StartEventStream(
                _supabase, callerId, eventId, HttpContext.RequestAborted);

            try
            {
                // The room's own cutoff backs the reaper's 4-hour limit (C11).
                var notAfter = DateTime.UtcNow + VideoStreamReaper.MaxStreamDuration + VideoStreamReaper.RoomGrace;
                await _sfu.CreateRoomAsync(stream.RoomName, _config.MaxParticipants, notAfter, CancellationToken.None);
            }
            catch
            {
                // A live row with no room would 409 every retry until the reaper noticed.
                await EndFailedStart(stream);
                throw;
            }

            await BroadcastSafely(
                () => _broadcaster.StreamStartedAsync(eventId, stream.Id, stream.StartedAt, CancellationToken.None),
                "stream_started", eventId);

            return Ok(await _videoStreams.RequireLiveEventStream(_supabase, eventId, CancellationToken.None));
        }

        /// <summary>
        /// Joins the live stream. Returns the stream, the caller's role, the roster and
        /// a short-lived room token: everything the client needs to connect and render
        /// in one round trip. A non-attendee is rejected before any token is minted.
        /// </summary>
        [HttpPost("event/{eventId}/join")]
        public async Task<IActionResult> JoinStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var (stream, participant, hadLiveConnection) = await _videoStreams.JoinEventStream(
                _supabase, callerId, eventId, HttpContext.RequestAborted);

            // One seat per user: Galène lets two connections share a username, so a
            // second tab or a reload replaces the older connection here.
            if (hadLiveConnection)
                await RemoveFromRoom(stream.RoomName, participant.SfuIdentity!, CancellationToken.None);

            // The role is read again right before minting: a demote that landed after
            // the join write must not be minted as publish rights.
            var (liveStream, current) = await _videoStreams.TouchLiveParticipant(
                _supabase, callerId, eventId, CancellationToken.None);
            var (token, canPublish) = MintToken(liveStream, current);

            var displayTask = _videoStreams.RequireLiveEventStream(_supabase, eventId, CancellationToken.None);
            var rosterTask = _videoStreams.GetParticipants(_supabase, liveStream.Id, CancellationToken.None);
            await Task.WhenAll(displayTask, rosterTask);
            var roster = await rosterTask;

            // The roster entry, never the token or SFU identity.
            var joined = roster.SingleOrDefault(p => p.ParticipantId == callerId);
            if (joined != null)
            {
                await BroadcastSafely(
                    () => _broadcaster.ParticipantJoinedAsync(eventId, joined, CancellationToken.None),
                    "participant_joined", eventId);
            }

            return Ok(new JoinVideoStreamResultDto
            {
                Stream = await displayTask,
                Role = current.Role,
                WsUrl = token.WsUrl,
                Group = token.Group,
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                CanPublish = canPublish,
                Participants = roster,
            });
        }

        /// <summary>
        /// A fresh room token for someone already live (slow first connect, or a
        /// reconnect that outlasted the 90 s token). No broadcast, no roster.
        /// 403 means the reaper removed the caller: join again.
        /// </summary>
        [HttpPost("event/{eventId}/token")]
        public async Task<IActionResult> RefreshToken(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var (stream, participant) = await _videoStreams.TouchLiveParticipant(
                _supabase, callerId, eventId, HttpContext.RequestAborted);
            var (token, canPublish) = MintToken(stream, participant);

            return Ok(new VideoTokenDto
            {
                Role = participant.Role,
                WsUrl = token.WsUrl,
                Group = token.Group,
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                CanPublish = canPublish,
            });
        }

        /// <summary>
        /// Leaves the stream: disconnects the caller on the SFU, then marks the row left
        /// and announces it. Idempotent, so every client exit path can call it (with
        /// fetch keepalive). A host who leaves does not end the stream, so a reload
        /// survives; the reaper ends a stream whose host is gone, and /end ends it now.
        /// </summary>
        [HttpPost("event/{eventId}/leave")]
        public async Task<IActionResult> LeaveStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var live = await _videoStreams.FindLiveParticipant(_supabase, callerId, eventId, HttpContext.RequestAborted);
            if (live == null)
                return NoContent();

            var (stream, participant) = live.Value;

            // Server-side removal is what stops the media (C9).
            if (!string.IsNullOrEmpty(participant.SfuIdentity))
                await RemoveFromRoom(stream.RoomName, participant.SfuIdentity, HttpContext.RequestAborted);

            var left = await _videoStreams.LeaveEventStream(
                _supabase, callerId, eventId, VideoParticipant.LeftExplicit, CancellationToken.None);

            // Null: someone else (the reaper, a kick) marked the row first and announces it.
            if (left.HasValue)
            {
                await BroadcastSafely(
                    () => _broadcaster.ParticipantLeftAsync(
                        eventId, callerId, VideoParticipant.LeftExplicit, CancellationToken.None),
                    "participant_left", eventId);
            }

            return NoContent();
        }

        /// <summary>
        /// Ends the stream for everyone. 409 when it was ended concurrently (by the reaper
        /// or another controller of the stream), who also announced it.
        /// </summary>
        [HttpPost("event/{eventId}/end")]
        public async Task<IActionResult> EndStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var stream = await _videoStreams.RequireStreamControl(_supabase, callerId, eventId, HttpContext.RequestAborted);

            // Deleting the room disconnects everyone. If it fails the stream stays live
            // and the end can be retried.
            await _sfu.DeleteRoomAsync(stream.RoomName, HttpContext.RequestAborted);

            var ended = await _videoStreams.EndStreamSystem(_supabase, stream, CancellationToken.None);

            await BroadcastSafely(
                () => _broadcaster.StreamEndedAsync(eventId, ended.Id, ended.EndedAt!.Value, CancellationToken.None),
                "stream_ended", eventId);

            return NoContent();
        }

        /// <summary>Clients call this every ~20 s, well under the 60 s reaper timeout.</summary>
        [HttpPost("event/{eventId}/heartbeat")]
        public async Task<IActionResult> Heartbeat(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            await _videoStreams.TouchLiveParticipant(_supabase, callerId, eventId, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpPost("event/{eventId}/camera")]
        public async Task<IActionResult> SetCameraState(
            Guid eventId, [FromBody] AlSaqrUpsertRequest<CameraStateForm> request)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            // MVC does not infer [Required] for the generic Values, so a body without it
            // binds as null.
            if (request.Values is null)
            {
                ModelState.AddModelError("values", "The camera state values are required.");
                return ValidationProblem(ModelState);
            }

            await _videoStreams.SetCameraState(
                _supabase, callerId, eventId, request.Values.CameraEnabled, request.Values.Muted,
                HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpPost("event/{eventId}/presenters/{userId}/promote")]
        public async Task<IActionResult> PromotePresenter(Guid eventId, Guid userId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var (stream, promoted) = await _videoStreams.PromotePresenter(
                _supabase, callerId, eventId, userId, _config.MaxPresenters, HttpContext.RequestAborted);

            if (!string.IsNullOrEmpty(promoted.SfuIdentity))
                await SetPublish(stream.RoomName, promoted.SfuIdentity, canPublish: true);

            await BroadcastSafely(
                () => _broadcaster.RoleChangedAsync(eventId, userId, promoted.Role, CancellationToken.None),
                "role_changed", eventId);
            return NoContent();
        }

        /// <summary>
        /// Presenter → viewer. Flipping the stored role is not enough: the live
        /// connection still has publish rights. SetPublishAsync revokes them on the SFU,
        /// effective immediately. If that fails the stored role is already viewer, so no
        /// new token can publish, and a retry revokes again.
        /// </summary>
        [HttpPost("event/{eventId}/presenters/{userId}/demote")]
        public async Task<IActionResult> DemotePresenter(Guid eventId, Guid userId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var (stream, demoted) = await _videoStreams.DemotePresenter(
                _supabase, callerId, eventId, userId, HttpContext.RequestAborted);

            if (!string.IsNullOrEmpty(demoted.SfuIdentity))
                await SetPublish(stream.RoomName, demoted.SfuIdentity, canPublish: false);

            await BroadcastSafely(
                () => _broadcaster.RoleChangedAsync(eventId, userId, demoted.Role, CancellationToken.None),
                "role_changed", eventId);
            return NoContent();
        }

        /// <summary>
        /// Presence/expiry gate, then the signature-verified caller id. Identity never
        /// comes from the shared logged-in-user cache slot (spec §0 DEP-2/DEP-3).
        /// </summary>
        private IActionResult? AuthorizeCaller(out Guid callerId)
        {
            callerId = Guid.Empty;

            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            callerId = _tokens.GetVerifiedUserId(Request.Headers.Authorization.ToString());
            return callerId == Guid.Empty ? Unauthorized("A verified access token is required.") : null;
        }

        /// <summary>
        /// The caller's own room token. canPublish comes from the role just read from the
        /// database (C4). The token is returned to the caller only: never logged, cached
        /// or broadcast.
        /// </summary>
        private (SfuJoinToken Token, bool CanPublish) MintToken(VideoStream stream, VideoParticipant participant)
        {
            var canPublish = participant.Role != VideoParticipant.RoleViewer;
            var identity = VideoParticipant.SfuIdentityFor(stream.Id, participant.ParticipantId);
            return (_sfu.MintJoinToken(stream.RoomName, identity, canPublish), canPublish);
        }

        private async Task RemoveFromRoom(string roomName, string identity, CancellationToken ct)
        {
            try
            {
                await _sfu.RemoveParticipantsAsync(roomName, new[] { identity }, ct);
            }
            catch (ParticipantNotConnectedException)
            {
                // Already gone: that is the outcome we wanted.
            }
        }

        private async Task SetPublish(string roomName, string identity, bool canPublish)
        {
            try
            {
                await _sfu.SetPublishAsync(roomName, identity, canPublish, CancellationToken.None);
            }
            catch (ParticipantNotConnectedException)
            {
                // No live connection to change. The stored role already holds, and the
                // next token minted for this user is read from it.
            }
        }

        private async Task EndFailedStart(VideoStream stream)
        {
            try
            {
                await _videoStreams.EndStreamSystem(_supabase, stream, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Never mask the room failure; the reaper ends any stream left live.
                _logger.LogError(ex, "Could not end video stream {StreamId} after its room failed to start.", stream.Id);
            }
        }

        private async Task BroadcastSafely(Func<Task> broadcast, string name, Guid eventId)
        {
            try
            {
                await broadcast();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broadcasting {Name} for event {EventId} failed.", name, eventId);
            }
        }
    }
}
