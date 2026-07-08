using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.Common;
using Supabase.Postgrest;
using static AlSaqr.Domain.SocialMedia.Spaces;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class SpaceRepository : ISpaceRepository
    {
        public SpaceRepository() { }

        // Community/discussion membership roles that may start or join a space.
        // "invited" / "requested" rows are NOT members yet and are rejected.
        private static readonly List<object> MemberRoles = new() { "member", "moderator", "founder" };

        public async Task<SpaceToDisplay?> GetLiveCommunitySpace(
            Supabase.Client supabase, Guid communityId, CancellationToken ct)
        {
            var space = await supabase
                .From<Space>()
                .Filter("community_id", Operator.Equals, communityId.ToString())
                .Filter("kind", Operator.Equals, Space.KindCommunity)
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

            return space == null ? null : await BuildSpaceToDisplay(supabase, space, ct);
        }

        public async Task<SpaceToDisplay?> GetLiveCommunityDiscussionSpace(
            Supabase.Client supabase, Guid communityId, Guid communityDiscussionId, CancellationToken ct)
        {
            var space = await supabase
                .From<Space>()
                .Filter("community_discussion_id", Operator.Equals, communityDiscussionId.ToString())
                .Filter("kind", Operator.Equals, Space.KindCommunityDiscussion)
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

            return space == null ? null : await BuildSpaceToDisplay(supabase, space, ct);
        }

        public async Task<SpaceToDisplay> StartCommunitySpace(
            Supabase.Client supabase, Guid userId, Guid communityId, string title, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ValidationException("A space requires a title.");

            if (!await IsCommunityMember(supabase, communityId, userId, ct))
                throw new ForbiddenException("Only community members may start a space in this community.");

            var live = await supabase
                .From<Space>()
                .Filter("community_id", Operator.Equals, communityId.ToString())
                .Filter("kind", Operator.Equals, Space.KindCommunity)
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

            if (live != null)
                throw new ConflictException("This community already has a live space.");

            var space = new Space
            {
                Id = Guid.NewGuid(),
                Kind = Space.KindCommunity,
                CommunityId = communityId,
                Title = title.Trim(),
                HostId = userId,
                StartedAt = DateTime.UtcNow,
            };

            return await InsertSpaceWithHost(supabase, space, userId, ct);
        }

        public async Task<SpaceToDisplay> StartCommunityDiscussionSpace(
            Supabase.Client supabase, Guid userId, Guid communityId, Guid communityDiscussionId, string title, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ValidationException("A space requires a title.");

            if (!await IsDiscussionMember(supabase, communityDiscussionId, userId, ct))
                throw new ForbiddenException("Only members of this discussion may start a space in it.");

            var live = await supabase
                .From<Space>()
                .Filter("community_discussion_id", Operator.Equals, communityDiscussionId.ToString())
                .Filter("kind", Operator.Equals, Space.KindCommunityDiscussion)
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

            if (live != null)
                throw new ConflictException("This discussion already has a live space.");

            var space = new Space
            {
                Id = Guid.NewGuid(),
                Kind = Space.KindCommunityDiscussion,
                CommunityId = communityId,
                CommunityDiscussionId = communityDiscussionId,
                Title = title.Trim(),
                HostId = userId,
                StartedAt = DateTime.UtcNow,
            };

            return await InsertSpaceWithHost(supabase, space, userId, ct);
        }

        public async Task<JoinSpaceResultDto> JoinSpace(
            Supabase.Client supabase, Guid userId, Guid spaceId, CancellationToken ct)
        {
            var space = await GetLiveSpaceOrThrow(supabase, spaceId, ct);

            // Membership is checked on every join — including while the space is
            // live — so a non-member can never enter mid-space.
            var isMember = space.Kind == Space.KindCommunityDiscussion
                ? await IsDiscussionMember(supabase, space.CommunityDiscussionId!.Value, userId, ct)
                : await IsCommunityMember(supabase, space.CommunityId, userId, ct);

            if (!isMember)
                throw new ForbiddenException("Only members may join this space.");

            var existing = await supabase
                .From<SpaceParticipant>()
                .Filter("space_id", Operator.Equals, spaceId.ToString())
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            var now = DateTime.UtcNow;
            if (existing == null)
            {
                existing = new SpaceParticipant
                {
                    Id = Guid.NewGuid(),
                    SpaceId = spaceId,
                    UserId = userId,
                    Role = space.HostId == userId ? SpaceParticipant.RoleHost : SpaceParticipant.RoleListener,
                    JoinedAt = now,
                    LastSeenAt = now,
                };
            }
            else
            {
                // Rejoin after leaving: the host keeps their role; anyone else
                // re-enters as a listener and must be re-approved to speak. The SFU
                // columns were already cleared by leave/reap. A duplicate join from
                // a participant that never left is idempotent — it must not reset
                // their role or destroy an active publish.
                if (existing.LeftAt != null)
                {
                    if (space.HostId != userId)
                        existing.Role = SpaceParticipant.RoleListener;

                    existing.HandRaised = false;
                    existing.SfuSessionId = null;
                    existing.TrackName = null;
                    existing.SfuMid = null;
                }

                existing.LeftAt = null;
                existing.LastSeenAt = now;
            }

            await supabase
                .From<SpaceParticipant>()
                .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            var participants = await BuildParticipantDtos(supabase, spaceId, ct);

            return new JoinSpaceResultDto
            {
                Space = await BuildSpaceToDisplay(supabase, space, ct),
                Role = existing.Role,
                Participants = participants,
                Speakers = participants
                    .Where(p => !string.IsNullOrEmpty(p.SfuSessionId) && !string.IsNullOrEmpty(p.TrackName))
                    .ToList(),
            };
        }

        public async Task<SpaceParticipant?> LeaveSpace(
            Supabase.Client supabase, Guid userId, Guid spaceId, CancellationToken ct)
        {
            var participant = await supabase
                .From<SpaceParticipant>()
                .Filter("space_id", Operator.Equals, spaceId.ToString())
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Single(ct);

            // Leave is idempotent: leaving a space you are not in is a no-op, since
            // every client exit path (unmount, tab close, explicit leave) calls it.
            if (participant == null)
                return null;

            var snapshot = Snapshot(participant);
            await MarkParticipantLeft(supabase, participant, ct);
            return snapshot;
        }

        public async Task<(Space Space, List<SpaceParticipant> Publishers)> EndSpace(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, CancellationToken ct)
        {
            var space = await GetLiveSpaceOrThrow(supabase, spaceId, ct);

            if (space.HostId != hostUserId)
                throw new ForbiddenException("Only the host may end a space.");

            var live = await GetLiveParticipants(supabase, spaceId, ct);
            var publishers = live
                .Where(p => !string.IsNullOrEmpty(p.SfuSessionId))
                .Select(Snapshot)
                .ToList();

            foreach (var participant in live)
                await MarkParticipantLeft(supabase, participant, ct);

            var ended = await EndSpaceSystem(supabase, space, ct);
            return (ended, publishers);
        }

        public async Task<SpaceParticipant> GetLiveParticipant(
            Supabase.Client supabase, Guid spaceId, Guid userId, CancellationToken ct)
        {
            await GetLiveSpaceOrThrow(supabase, spaceId, ct);

            var participant = await supabase
                .From<SpaceParticipant>()
                .Filter("space_id", Operator.Equals, spaceId.ToString())
                .Filter("user_id", Operator.Equals, userId.ToString())
                // .Filter("left_at", Operator.Is, "null")
                .Single(ct);

            if (participant == null)
                throw new ForbiddenException("You are not a participant of this space.");

            // Any authenticated space call proves liveness — feeds the reaper.
            participant.LastSeenAt = DateTime.UtcNow;
            await supabase
                .From<SpaceParticipant>()
                .Upsert(participant, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return participant;
        }

        public async Task<SpaceParticipant> GetPublisherParticipant(
            Supabase.Client supabase, Guid spaceId, Guid userId, CancellationToken ct)
        {
            var participant = await GetLiveParticipant(supabase, spaceId, userId, ct);

            // Publish-time role check: the persisted role decides, not the client's
            // view of itself. A listener that flipped its own role gets 403 here and
            // no SFU call is ever made.
            if (participant.Role != SpaceParticipant.RoleHost && participant.Role != SpaceParticipant.RoleSpeaker)
                throw new ForbiddenException("Only the host and approved speakers may publish audio.");

            return participant;
        }

        public async Task SetPublishState(
            Supabase.Client supabase, Guid spaceId, Guid userId,
            string sfuSessionId, string trackName, string sfuMid, CancellationToken ct)
        {
            var participant = await GetLiveParticipant(supabase, spaceId, userId, ct);

            participant.SfuSessionId = sfuSessionId;
            participant.TrackName = trackName;
            participant.SfuMid = sfuMid;

            await supabase
                .From<SpaceParticipant>()
                .Upsert(participant, new QueryOptions { Returning = ReturnType.Minimal }, ct);
        }

        public async Task SetHandRaised(
            Supabase.Client supabase, Guid spaceId, Guid userId, bool raised, CancellationToken ct)
        {
            var participant = await GetLiveParticipant(supabase, spaceId, userId, ct);

            participant.HandRaised = raised;

            await supabase
                .From<SpaceParticipant>()
                .Upsert(participant, new QueryOptions { Returning = ReturnType.Minimal }, ct);
        }

        public async Task<SpaceParticipant> ApproveSpeaker(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, Guid targetUserId, CancellationToken ct)
        {
            var target = await GetTargetForHostAction(supabase, hostUserId, spaceId, targetUserId, ct);

            target.Role = SpaceParticipant.RoleSpeaker;
            target.HandRaised = false;

            await supabase
                .From<SpaceParticipant>()
                .Upsert(target, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return target;
        }

        public async Task<SpaceParticipant> DemoteSpeaker(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, Guid targetUserId, CancellationToken ct)
        {
            var target = await GetTargetForHostAction(supabase, hostUserId, spaceId, targetUserId, ct);

            if (target.Role == SpaceParticipant.RoleHost)
                throw new ValidationException("The host cannot be demoted.");

            var snapshot = Snapshot(target);

            target.Role = SpaceParticipant.RoleListener;
            target.SfuSessionId = null;
            target.TrackName = null;
            target.SfuMid = null;

            await supabase
                .From<SpaceParticipant>()
                .Upsert(target, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return snapshot;
        }

        public async Task<List<Space>> GetLiveSpaces(Supabase.Client supabase, CancellationToken ct)
        {
            return (await supabase
                .From<Space>()
                .Filter("ended_at", Operator.Is, "null")
                .Order("started_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct)).Models;
        }

        public async Task<List<SpaceParticipant>> GetLiveParticipants(
            Supabase.Client supabase, Guid spaceId, CancellationToken ct)
        {
            return (await supabase
                .From<SpaceParticipant>()
                .Filter("space_id", Operator.Equals, spaceId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Order("joined_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct)).Models;
        }

        public async Task MarkParticipantLeft(
            Supabase.Client supabase, SpaceParticipant participant, CancellationToken ct)
        {
            participant.LeftAt = DateTime.UtcNow;
            participant.SfuSessionId = null;
            participant.TrackName = null;
            participant.SfuMid = null;

            await supabase
                .From<SpaceParticipant>()
                .Upsert(participant, new QueryOptions { Returning = ReturnType.Minimal }, ct);
        }

        public async Task<Space> EndSpaceSystem(Supabase.Client supabase, Space space, CancellationToken ct)
        {
            space.EndedAt = DateTime.UtcNow;

            await supabase
                .From<Space>()
                .Upsert(space, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return space;
        }

        // ----- helpers -----

        private async Task<SpaceToDisplay> InsertSpaceWithHost(
            Supabase.Client supabase, Space space, Guid hostUserId, CancellationToken ct)
        {
            var created = (await supabase
                .From<Space>()
                .Insert(space, new QueryOptions { Returning = ReturnType.Representation }, ct))
                .Models.FirstOrDefault()
                ?? throw new ConflictException("The space could not be created.");

            var host = new SpaceParticipant
            {
                Id = Guid.NewGuid(),
                SpaceId = created.Id,
                UserId = hostUserId,
                Role = SpaceParticipant.RoleHost,
                JoinedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };

            await supabase
                .From<SpaceParticipant>()
                .Upsert(host, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return await BuildSpaceToDisplay(supabase, created, ct);
        }

        private async Task<Space> GetLiveSpaceOrThrow(
            Supabase.Client supabase, Guid spaceId, CancellationToken ct)
        {
            var space = await supabase
                .From<Space>()
                .Filter("id", Operator.Equals, spaceId.ToString())
                .Single(ct);

            if (space == null || space.EndedAt != null)
                throw new NotFoundException($"Space {spaceId} does not exist or has ended.");

            return space;
        }

        private async Task<SpaceParticipant> GetTargetForHostAction(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, Guid targetUserId, CancellationToken ct)
        {
            var space = await GetLiveSpaceOrThrow(supabase, spaceId, ct);

            if (space.HostId != hostUserId)
                throw new ForbiddenException("Only the host may change speaker roles.");

            var target = await supabase
                .From<SpaceParticipant>()
                .Filter("space_id", Operator.Equals, spaceId.ToString())
                .Filter("user_id", Operator.Equals, targetUserId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Single(ct);

            if (target == null)
                throw new NotFoundException($"User {targetUserId} is not a participant of this space.");

            return target;
        }

        private async Task<bool> IsCommunityMember(
            Supabase.Client supabase, Guid communityId, Guid userId, CancellationToken ct)
        {
            var member = await supabase
                .From<CommunityMember>()
                .Filter("community_id", Operator.Equals, communityId.ToString())
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Filter("role", Operator.In, MemberRoles)
                .Single(ct);

            return member != null;
        }

        private async Task<bool> IsDiscussionMember(
            Supabase.Client supabase, Guid communityDiscussionId, Guid userId, CancellationToken ct)
        {
            var member = await supabase
                .From<CommunityDiscussionMember>()
                .Filter("community_discussion_id", Operator.Equals, communityDiscussionId.ToString())
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Filter("role", Operator.In, MemberRoles)
                .Single(ct);

            return member != null;
        }

        private async Task<SpaceToDisplay> BuildSpaceToDisplay(
            Supabase.Client supabase, Space space, CancellationToken ct)
        {
            var host = await supabase
                .From<AlSaqrUser>()
                .Filter("id", Operator.Equals, space.HostId.ToString())
                .Single(ct);

            var participantCount = (await GetLiveParticipants(supabase, space.Id, ct)).Count;

            return new SpaceToDisplay
            {
                SpaceId = space.Id,
                Kind = space.Kind,
                CommunityId = space.CommunityId,
                CommunityDiscussionId = space.CommunityDiscussionId,
                Title = space.Title,
                HostId = space.HostId,
                HostUsername = host?.Username,
                HostAvatar = host?.Avatar,
                StartedAt = space.StartedAt,
                EndedAt = space.EndedAt,
                ParticipantCount = participantCount,
                IsLive = space.EndedAt == null,
            };
        }

        private async Task<List<SpaceParticipantDto>> BuildParticipantDtos(
            Supabase.Client supabase, Guid spaceId, CancellationToken ct)
        {
            var participants = await GetLiveParticipants(supabase, spaceId, ct);
            if (participants.Count == 0)
                return new List<SpaceParticipantDto>();

            var userIds = participants.Select(p => (object)p.UserId.ToString()).ToList();
            var users = (await supabase
                .From<AlSaqrUser>()
                .Filter("id", Operator.In, userIds)
                .Get(ct)).Models.ToDictionary(u => u.Id);

            return participants
                .Select(p => new SpaceParticipantDto
                {
                    UserId = p.UserId,
                    Username = users.TryGetValue(p.UserId, out var user) ? user.Username : string.Empty,
                    Avatar = users.TryGetValue(p.UserId, out var u2) ? u2.Avatar : null,
                    Role = p.Role,
                    Muted = p.Muted,
                    HandRaised = p.HandRaised,
                    SfuSessionId = p.SfuSessionId,
                    TrackName = p.TrackName,
                })
                .ToList();
        }

        /// <summary>
        /// Detached copy carrying the SFU state a caller needs after the row has
        /// been mutated (e.g. to close the published track post-demotion/leave).
        /// </summary>
        private static SpaceParticipant Snapshot(SpaceParticipant p) => new()
        {
            Id = p.Id,
            SpaceId = p.SpaceId,
            UserId = p.UserId,
            Role = p.Role,
            Muted = p.Muted,
            HandRaised = p.HandRaised,
            SfuSessionId = p.SfuSessionId,
            TrackName = p.TrackName,
            SfuMid = p.SfuMid,
            JoinedAt = p.JoinedAt,
            LeftAt = p.LeftAt,
            LastSeenAt = p.LastSeenAt,
        };
    }
}
