using System.Globalization;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Common;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using static AlSaqr.Domain.Meetup.EventVideo;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    /// <summary>
    /// Video streams for online events (specs/video-streaming-online-events.md §6).
    /// The backend is authoritative for attendance and roles: every gate is checked
    /// here, before the controller makes any SFU call or mints any token.
    /// </summary>
    public class VideoStreamRepository : IVideoStreamRepository
    {
        public async Task<VideoStreamToDisplay?> GetLiveEventStream(
            Supabase.Client supabase, Guid eventId, CancellationToken ct)
        {
            var stream = await FindLiveStream(supabase, eventId, ct);
            return stream == null ? null : await ToDisplay(supabase, stream, ct);
        }

        public async Task<VideoStreamToDisplay?> GetLiveEventStreamForUser(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            await EnsureMayWatch(supabase, hostEvent, userId, ct);
            return await GetLiveEventStream(supabase, eventId, ct);
        }

        public async Task<VideoStreamToDisplay> RequireLiveEventStream(
            Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            await ToDisplay(supabase, await GetLiveStreamOrThrow(supabase, eventId, ct), ct);

        /// <summary>
        /// The pre-check is an optimisation; the partial unique index is the guard. Two
        /// concurrent starts both pass the pre-check and PostgREST returns 23505 for the
        /// loser, which must surface as 409, not 500.
        /// </summary>
        public async Task<VideoStream> StartEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);

            if (!await CanControlEventVideo(supabase, hostEvent, userId, ct))
                throw new ForbiddenException("Only the event organizer or group founder may start the video stream.");

            if (await FindLiveStream(supabase, eventId, ct) != null)
                throw new ConflictException("This event already has a live video stream.");

            var streamId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            VideoStream created;
            try
            {
                created = (await supabase
                    .From<VideoStream>()
                    .Insert(
                        new VideoStream
                        {
                            Id = streamId,
                            EventId = eventId,
                            RoomName = VideoStream.RoomNameFor(streamId),   // stream-scoped (§4)
                            HostId = userId,
                            StartedAt = now,
                        },
                        new QueryOptions { Returning = ReturnType.Representation },
                        ct))
                    .Models.FirstOrDefault()
                    ?? throw new NotFoundException("The created video stream was not returned.");
            }
            catch (PostgrestException ex) when (PostgrestErrors.IsUniqueViolation(ex))
            {
                // Lost the race against a concurrent start.
                throw new ConflictException("This event already has a live video stream.");
            }

            try
            {
                _ = (await supabase
                        .From<VideoParticipant>()
                        .Insert(
                            new VideoParticipant
                            {
                                Id = Guid.NewGuid(),
                                EventId = eventId,
                                ParticipantId = userId,
                                VideoStreamId = created.Id,
                                Role = VideoParticipant.RoleHost,
                                // No SFU identity until the host actually joins: it marks "has a
                                // token and may be connected", which drives second-device kicks.
                                JoinedAt = now,
                                LastSeenAt = now,
                            },
                            new QueryOptions { Returning = ReturnType.Representation },
                            ct))
                    .Models.FirstOrDefault()
                    ?? throw new NotFoundException("The created host participant was not returned.");
            }
            catch
            {
                // Never leave a host-less live stream behind.
                // Compensation must outlive a cancelled request and must never mask the
                // original failure, so it ignores ct and swallows its own errors.
                try
                {
                    await EndStreamSystem(supabase, created, CancellationToken.None);
                }
                catch
                {
                    // The reaper ends any stream left live; surface the original exception.
                }

                throw;
            }

            return created;
        }

        public Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct) =>
            EndStream(supabase, stream, VideoParticipant.LeftEnded, ct);

        public async Task<List<VideoParticipant>> GetLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Order("joined_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task MarkParticipantLeft(
            Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct) =>
            _ = await TryMarkLeft(supabase, participant.Id, reason, ct);

        /// <summary>
        /// Column-scoped and guarded on left_at is null: never writes back a stale role,
        /// and a row that already left (or was concurrently reaped) is left alone.
        /// Returns the updated row, or null when it was not live.
        /// </summary>
        private static async Task<VideoParticipant?> TryMarkLeft(
            Supabase.Client supabase, Guid participantRowId, string reason, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("id", Operator.Equals, participantRowId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Set(p => p.LeftAt!, DateTime.UtcNow)
                .Set(p => p.LeftReason!, reason)
                .Set(p => p.SfuIdentity!, null)
                .Update(new QueryOptions { Returning = ReturnType.Representation }, ct))
            .Models.FirstOrDefault();

        private static readonly TimeSpan RejoinGrace = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Role on (re)join: the host resumes 'host' (after a kick, only while they
        /// still control the event's video); a participant who left within the grace
        /// window resumes their persisted role; anyone else is 'viewer'. A join while
        /// already live (second tab, reload) leaves the role untouched and reports
        /// HadLiveConnection, so the controller kicks the older SFU connection: one
        /// seat per user.
        /// </summary>
        public async Task<(VideoStream Stream, VideoParticipant Participant, bool HadLiveConnection)> JoinEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

            // Re-checked on EVERY join.
            await EnsureMayWatch(supabase, hostEvent, userId, ct);

            var now = DateTime.UtcNow;
            var sfuIdentity = VideoParticipant.SfuIdentityFor(stream.Id, userId);

            var existing = await FindParticipant(supabase, stream.Id, userId, ct);
            if (existing != null)
                return await JoinExistingRow(supabase, hostEvent, stream, existing, sfuIdentity, now, ct);

            await BeforeJoinWrite(stream.Id, userId);
            try
            {
                var created = (await supabase
                        .From<VideoParticipant>()
                        .Insert(
                            new VideoParticipant
                            {
                                Id = Guid.NewGuid(),
                                EventId = eventId,
                                ParticipantId = userId,
                                VideoStreamId = stream.Id,
                                Role = stream.HostId == userId ? VideoParticipant.RoleHost : VideoParticipant.RoleViewer,
                                SfuIdentity = sfuIdentity,
                                JoinedAt = now,
                                LastSeenAt = now,
                            },
                            new QueryOptions { Returning = ReturnType.Representation },
                            ct))
                    .Models.FirstOrDefault()
                    ?? throw new NotFoundException("The video participant was not returned.");

                return (stream, created, false);
            }
            catch (PostgrestException ex) when (PostgrestErrors.IsUniqueViolation(ex))
            {
                // Lost the race against a concurrent first join: take the existing-row path.
                var winner = await FindParticipant(supabase, stream.Id, userId, ct)
                    ?? throw new ConflictException("Could not join the video stream. Try again.");
                return await JoinExistingRow(supabase, hostEvent, stream, winner, sfuIdentity, now, ct);
            }
        }

        /// <summary>
        /// Test seam: runs after the join has read the participant row (or found none)
        /// and before it writes, so tests can interleave a concurrent change.
        /// </summary>
        protected virtual Task BeforeJoinWrite(Guid videoStreamId, Guid userId) => Task.CompletedTask;

        private static Task<VideoParticipant?> FindParticipant(
            Supabase.Client supabase, Guid videoStreamId, Guid userId, CancellationToken ct) =>
            supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("participant_id", Operator.Equals, userId.ToString())
                .Single(ct);

        /// <summary>
        /// Column-scoped, guarded writes: a stale read can never overwrite a concurrent
        /// demote, camera change or leave. Already live: only sfu_identity and
        /// last_seen_at change. Rejoin: the row is revived (guarded on left_at not null)
        /// and role is written only to downgrade to viewer, or to give a kicked host the
        /// host role back; a kept role is never written.
        /// An empty result means the row changed under us: Conflict, the client retries.
        /// </summary>
        private async Task<(VideoStream Stream, VideoParticipant Participant, bool HadLiveConnection)> JoinExistingRow(
            Supabase.Client supabase, Event hostEvent, VideoStream stream, VideoParticipant existing,
            string sfuIdentity, DateTime now, CancellationToken ct)
        {
            var hadLiveConnection = existing is { LeftAt: null, SfuIdentity: not null };
            var isStreamHost = stream.HostId == existing.ParticipantId;
            var keepsRole = existing.LeftAt == null
                || isStreamHost
                || await LeftWithinGrace(supabase, existing.Id, now, ct);

            // A kick resets the role to viewer. The stream's host takes the host role back
            // on rejoin only while they still control the event's video.
            var restoresHost = existing.LeftAt != null
                && isStreamHost
                && existing.Role != VideoParticipant.RoleHost
                && await CanControlEventVideo(supabase, hostEvent, existing.ParticipantId, ct);

            await BeforeJoinWrite(stream.Id, existing.ParticipantId);

            var query = supabase
                .From<VideoParticipant>()
                .Filter("id", Operator.Equals, existing.Id.ToString());

            if (existing.LeftAt == null)
            {
                query = query.Filter("left_at", Operator.Is, "null");
            }
            else
            {
                query = query
                    .Not("left_at", Operator.Is, "null")
                    .Set(p => p.LeftAt!, null)
                    .Set(p => p.LeftReason!, null);
                if (restoresHost)
                    query = query.Set(p => p.Role, VideoParticipant.RoleHost);
                else if (!keepsRole)
                    query = query.Set(p => p.Role, VideoParticipant.RoleViewer);
            }

            var updated = (await query
                    .Set(p => p.SfuIdentity!, sfuIdentity)
                    .Set(p => p.LastSeenAt, now)
                    .Update(new QueryOptions { Returning = ReturnType.Representation }, ct))
                .Models.FirstOrDefault()
                ?? throw new ConflictException("The video participant changed during join. Try again.");

            return (stream, updated, hadLiveConnection);
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)?> FindLiveParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var stream = await FindLiveStream(supabase, eventId, ct);
            if (stream == null)
                return null;

            var live = await FindLiveRow(supabase, stream.Id, userId, ct);
            return live == null ? null : (stream, live);
        }

        public async Task<(VideoStream Stream, VideoParticipant Snapshot)?> LeaveEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, string reason, CancellationToken ct)
        {
            var found = await FindLiveParticipant(supabase, userId, eventId, ct);
            if (found == null)
                return null;

            var (stream, live) = found.Value;
            var snapshot = Copy(live);
            return await TryMarkLeft(supabase, live.Id, reason, ct) == null ? null : (stream, snapshot);
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)> TouchLiveParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

            // Same gate as join, before the row is kept alive or a token is minted from
            // it: a removed attendee or revoked organizer can no longer stay connected.
            await EnsureMayWatch(supabase, hostEvent, userId, ct);

            var updated = await LiveRowQuery(supabase, stream.Id, userId)
                .Set(p => p.LastSeenAt, DateTime.UtcNow)
                .Update(new QueryOptions { Returning = ReturnType.Representation }, ct);

            return (stream, updated.Models.FirstOrDefault() ?? throw NotLive());
        }

        public async Task SetCameraState(
            Supabase.Client supabase, Guid userId, Guid eventId, bool cameraEnabled, bool muted, CancellationToken ct)
        {
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);
            var updated = await LiveRowQuery(supabase, stream.Id, userId)
                .Set(p => p.CameraEnabled, cameraEnabled)
                .Set(p => p.Muted, muted)
                .Set(p => p.LastSeenAt, DateTime.UtcNow)
                .Update(new QueryOptions { Returning = ReturnType.Representation }, ct);

            _ = updated.Models.FirstOrDefault() ?? throw NotLive();
        }

        public async Task<List<VideoParticipantDto>> GetParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct)
        {
            var rows = await GetLiveParticipants(supabase, videoStreamId, ct);
            if (rows.Count == 0)
                return new List<VideoParticipantDto>();

            var users = (await supabase
                    .From<AlSaqrUser>()
                    .Filter("id", Operator.In, rows.Select(r => r.ParticipantId.ToString()).ToList())
                    .Get(ct))
                .Models
                .ToDictionary(u => u.Id);

            return rows
                .Select(r =>
                {
                    users.TryGetValue(r.ParticipantId, out var user);
                    return new VideoParticipantDto
                    {
                        ParticipantId = r.ParticipantId,
                        Username = user?.Username ?? string.Empty,
                        Avatar = user?.Avatar,
                        Role = r.Role,
                        CameraEnabled = r.CameraEnabled,
                        Muted = r.Muted,
                    };
                })
                .ToList();
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)> PromotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, int maxPresenters, CancellationToken ct)
        {
            var (stream, target) = await GetControlledTarget(supabase, callerId, eventId, targetUserId, ct);

            // Host and presenters already publish.
            if (target.Role != VideoParticipant.RoleViewer)
                return (stream, target);

            var publishers = await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Filter("role", Operator.In, new List<string> { VideoParticipant.RoleHost, VideoParticipant.RolePresenter })
                .Count(Constants.CountType.Exact, ct);

            if (publishers >= maxPresenters)
                throw new ConflictException($"This stream already has the maximum of {maxPresenters} presenters.");

            var promoted = await ChangeRole(
                supabase, target.Id, VideoParticipant.RoleViewer, VideoParticipant.RolePresenter, ct);

            return (stream, promoted);
        }

        public async Task<(VideoStream Stream, VideoParticipant Target)> RequireDemoteTarget(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct)
        {
            var (stream, target) = await GetControlledTarget(supabase, callerId, eventId, targetUserId, ct);

            if (target.Role == VideoParticipant.RoleHost)
                throw new ValidationException("The host cannot be demoted.");

            return (stream, target);
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)> DemotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct)
        {
            var (stream, target) = await RequireDemoteTarget(supabase, callerId, eventId, targetUserId, ct);

            if (target.Role == VideoParticipant.RoleViewer)
                return (stream, target);

            var demoted = await ChangeRole(
                supabase, target.Id, VideoParticipant.RolePresenter, VideoParticipant.RoleViewer, ct);

            return (stream, demoted);
        }

        /// <summary>
        /// Column-scoped role change guarded on the row id, the expected current role and
        /// left_at is null: only role is written, and a concurrent leave, kick or role
        /// change makes it a Conflict instead of resurrecting a stale value.
        /// </summary>
        private static async Task<VideoParticipant> ChangeRole(
            Supabase.Client supabase, Guid participantRowId, string fromRole, string toRole, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("id", Operator.Equals, participantRowId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Filter("role", Operator.Equals, fromRole)
                .Set(p => p.Role, toRole)
                .Update(new QueryOptions { Returning = ReturnType.Representation }, ct))
            .Models.FirstOrDefault()
            ?? throw new ConflictException("The video participant changed. Try again.");

        public async Task<VideoStream> RequireStreamControl(
            Supabase.Client supabase, Guid callerId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

            if (stream.HostId != callerId && !await CanControlEventVideo(supabase, hostEvent, callerId, ct))
                throw new ForbiddenException(
                    "Only the stream host, event organizer, or group founder may control the video stream.");

            return stream;
        }

        public virtual async Task<(VideoStream Stream, VideoParticipant Participant)?> KickParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var stream = await FindLiveStream(supabase, eventId, ct);
            if (stream == null)
                return null;

            var kicked = (await LiveRowQuery(supabase, stream.Id, userId)
                    .Set(p => p.LeftAt!, DateTime.UtcNow)
                    .Set(p => p.LeftReason!, VideoParticipant.LeftKicked)
                    .Set(p => p.Role, VideoParticipant.RoleViewer)
                    .Set(p => p.SfuIdentity!, null)
                    .Update(new QueryOptions { Returning = ReturnType.Representation }, ct))
                .Models.FirstOrDefault();

            return kicked == null ? null : (stream, kicked);
        }

        public async Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct) =>
            (await supabase
                .From<VideoStream>()
                .Filter("ended_at", Operator.Is, "null")
                .Order("started_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task<List<VideoStream>> GetLiveStreamsStartedBefore(
            Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct) =>
            (await supabase
                .From<VideoStream>()
                .Filter("ended_at", Operator.Is, "null")
                .Filter("started_at", Operator.LessThan, ToUtcIso(cutoffUtc))
                .Order("started_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task<List<VideoParticipant>> GetStaleLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, DateTime cutoffUtc, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Filter("last_seen_at", Operator.LessThan, ToUtcIso(cutoffUtc))
                .Order("joined_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task<int> PurgeParticipantsLeftBefore(
            Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct)
        {
            var cutoff = ToUtcIso(cutoffUtc);

            var doomed = await supabase
                .From<VideoParticipant>()
                .Filter("left_at", Operator.LessThan, cutoff)
                .Count(Constants.CountType.Exact, ct);

            if (doomed > 0)
            {
                await supabase
                    .From<VideoParticipant>()
                    .Filter("left_at", Operator.LessThan, cutoff)
                    .Delete(new QueryOptions { Returning = ReturnType.Minimal }, ct);
            }

            // Streams that ended before the cutoff are now unreferenced; any straggling
            // participant row cascades with its stream.
            await supabase
                .From<VideoStream>()
                .Filter("ended_at", Operator.LessThan, cutoff)
                .Delete(new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return doomed;
        }

        private async Task<(VideoStream Stream, VideoParticipant Target)> GetControlledTarget(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct)
        {
            var stream = await RequireStreamControl(supabase, callerId, eventId, ct);

            var target = await FindLiveRow(supabase, stream.Id, targetUserId, ct)
                ?? throw new NotFoundException("That user is not live on this video stream.");

            return (stream, target);
        }

        // ----- stream lifecycle -----

        private static Task<VideoParticipant?> FindLiveRow(
            Supabase.Client supabase, Guid videoStreamId, Guid userId, CancellationToken ct) =>
            supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("participant_id", Operator.Equals, userId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Single(ct);

        // Column-scoped updates are guarded on left_at is null and never write role,
        // sfu_identity or left_at, so a stale read cannot revive or re-promote a row.
        private static Supabase.Postgrest.Interfaces.IPostgrestTable<VideoParticipant> LiveRowQuery(
            Supabase.Client supabase, Guid videoStreamId, Guid userId) =>
            supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("participant_id", Operator.Equals, userId.ToString())
                .Filter("left_at", Operator.Is, "null");

        // The 403 detail is part of the client contract: "not live -> call join again".
        private static ForbiddenException NotLive() =>
            new("You are not live on this video stream. Join again.");

        // Evaluated in SQL, never by comparing a deserialized DateTime in C#.
        private static async Task<bool> LeftWithinGrace(
            Supabase.Client supabase, Guid participantRowId, DateTime utcNow, CancellationToken ct) =>
            await supabase
                .From<VideoParticipant>()
                .Filter("id", Operator.Equals, participantRowId.ToString())
                // Only a network-drop reap restores a role; explicit/ended/expired never do.
                .Filter("left_reason", Operator.Equals, VideoParticipant.LeftReaped)
                .Filter("left_at", Operator.GreaterThanOrEqual, ToUtcIso(utcNow - RejoinGrace))
                .Single(ct) != null;

        private static VideoParticipant Copy(VideoParticipant p) => new()
        {
            Id = p.Id,
            EventId = p.EventId,
            ParticipantId = p.ParticipantId,
            VideoStreamId = p.VideoStreamId,
            Role = p.Role,
            CameraEnabled = p.CameraEnabled,
            Muted = p.Muted,
            SfuIdentity = p.SfuIdentity,
            JoinedAt = p.JoinedAt,
            LeftAt = p.LeftAt,
            LeftReason = p.LeftReason,
            LastSeenAt = p.LastSeenAt,
        };

        /// <summary>
        /// Column-scoped and guarded on ended_at is null: a stream ended concurrently
        /// is a Conflict, never silently re-ended or overwritten from a stale read.
        /// </summary>
        private async Task<VideoStream> EndStream(
            Supabase.Client supabase, VideoStream stream, string reason, CancellationToken ct)
        {
            foreach (var participant in await GetLiveParticipants(supabase, stream.Id, ct))
                await MarkParticipantLeft(supabase, participant, reason, ct);

            return (await supabase
                    .From<VideoStream>()
                    .Filter("id", Operator.Equals, stream.Id.ToString())
                    .Filter("ended_at", Operator.Is, "null")
                    .Set(s => s.EndedAt!, DateTime.UtcNow)
                    .Update(new QueryOptions { Returning = ReturnType.Representation }, ct))
                .Models.FirstOrDefault()
                ?? throw new ConflictException($"Video stream {stream.Id} has already ended.");
        }

        private static Task<VideoStream?> FindLiveStream(Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            supabase
                .From<VideoStream>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

        private static async Task<VideoStream> GetLiveStreamOrThrow(
            Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            await FindLiveStream(supabase, eventId, ct)
            ?? throw new NotFoundException("No live video stream for this event.");

        private static async Task<VideoStreamToDisplay> ToDisplay(
            Supabase.Client supabase, VideoStream stream, CancellationToken ct)
        {
            var hostEvent = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, stream.EventId.ToString())
                .Single(ct);

            var host = await supabase
                .From<AlSaqrUser>()
                .Filter("id", Operator.Equals, stream.HostId.ToString())
                .Single(ct);

            var participantCount = await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Count(Constants.CountType.Exact, ct);

            return new VideoStreamToDisplay
            {
                VideoStreamId = stream.Id,
                EventId = stream.EventId,
                EventName = hostEvent?.Name,
                HostId = stream.HostId,
                HostUsername = host?.Username,
                HostAvatar = host?.Avatar,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                ParticipantCount = participantCount,
                IsLive = stream.EndedAt == null,
            };
        }

        // ----- authorization gates (spec §6.1) -----

        // C1: online events only, checked before any SFU call. A groupless event has
        // no attendees (event_attendees.group_id is NOT NULL), so no one to authorize.
        private static async Task<Event> GetOnlineEventOrThrow(
            Supabase.Client supabase, Guid eventId, CancellationToken ct)
        {
            var existing = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, eventId.ToString())
                .Single(ct)
                ?? throw new NotFoundException($"Event {eventId} does not exist.");

            if (!existing.IsOnline)
                throw new ValidationException("Video streaming is available for online events only.");

            if (existing.GroupId is null)
                throw new ValidationException("This event has no host group and cannot host video.");

            return existing;
        }

        // Who may see or join an event's stream: its attendees, plus whoever may control
        // its video (a founder can start a stream without being an attendee).
        private static async Task EnsureMayWatch(
            Supabase.Client supabase, Event hostEvent, Guid userId, CancellationToken ct)
        {
            if (!await IsEventAttendee(supabase, hostEvent.Id, userId, ct)
                && !await CanControlEventVideo(supabase, hostEvent, userId, ct))
                throw new ForbiddenException("Only users attending this event may join its video stream.");
        }

        // Attendance lives on event_attendees, keyed on attendees.id.
        private static async Task<bool> IsEventAttendee(
            Supabase.Client supabase, Guid eventId, Guid userId, CancellationToken ct)
        {
            var attendee = await supabase
                .From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (attendee == null)
                return false;

            var attending = await supabase
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Single(ct);

            return attending != null;
        }

        // Two legitimate authorities: the host group's founder (checked first, the
        // escalation path) and the event organizer.
        private static async Task<bool> CanControlEventVideo(
            Supabase.Client supabase, Event hostEvent, Guid userId, CancellationToken ct)
        {
            var founded = await supabase
                .From<Groups>()
                .Filter("id", Operator.Equals, hostEvent.GroupId!.Value.ToString())
                .Filter("founder_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (founded != null)
                return true;

            var attendee = await supabase
                .From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (attendee == null)
                return false;

            var organizer = await supabase
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, hostEvent.Id.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Filter("is_event_organizer", Operator.Equals, "true")
                .Single(ct);

            return organizer != null;
        }

        /// <summary>UTC ISO-8601 with microseconds, for PostgREST timestamp filters.</summary>
        private static string ToUtcIso(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    }
}
