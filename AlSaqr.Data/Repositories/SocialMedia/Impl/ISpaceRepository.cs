using AlSaqr.Data.Entities.SocialMedia;
using static AlSaqr.Domain.SocialMedia.Spaces;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    /// <summary>
    /// Data access + authorization for ephemeral audio spaces
    /// (specs/audio-spaces.md). The backend is authoritative for roles and
    /// permissions: every method that guards an action throws the HTTP-mapped
    /// exceptions (NotFound/Forbidden/Conflict/Validation) from inside the
    /// repository, per CLAUDE.md §3.3. Nothing here is cached — spaces are
    /// real-time state and are always read live (§4.1 exemption).
    /// </summary>
    public interface ISpaceRepository
    {
        /// <summary>Live space for a community, or null when none is live.</summary>
        Task<SpaceToDisplay?> GetLiveCommunitySpace(
            Supabase.Client supabase, Guid communityId, CancellationToken ct);

        /// <summary>Live space for a community discussion, or null.</summary>
        Task<SpaceToDisplay?> GetLiveCommunityDiscussionSpace(
            Supabase.Client supabase, Guid communityId, Guid communityDiscussionId, CancellationToken ct);

        /// <summary>
        /// Starts a community space; the caller becomes host. Throws Forbidden for
        /// non-members and Conflict when the community already has a live space.
        /// </summary>
        Task<SpaceToDisplay> StartCommunitySpace(
            Supabase.Client supabase, Guid userId, Guid communityId, string title, CancellationToken ct);

        /// <summary>
        /// Starts a community-discussion space; the caller becomes host. Throws
        /// Forbidden for non-members of the discussion and Conflict when the
        /// discussion already has a live space.
        /// </summary>
        Task<SpaceToDisplay> StartCommunityDiscussionSpace(
            Supabase.Client supabase, Guid userId, Guid communityId, Guid communityDiscussionId, string title, CancellationToken ct);

        /// <summary>
        /// Joins a live space as listener (host keeps their role on rejoin). Throws
        /// NotFound when the space does not exist or has ended, Forbidden when the
        /// caller is not a member of the community/discussion.
        /// </summary>
        Task<JoinSpaceResultDto> JoinSpace(
            Supabase.Client supabase, Guid userId, Guid spaceId, CancellationToken ct);

        /// <summary>
        /// Marks the caller as left and clears their SFU state. Returns the
        /// participant as it was before leaving (so the caller's published track can
        /// be closed on the SFU), or null when the user was not a live participant.
        /// </summary>
        Task<SpaceParticipant?> LeaveSpace(
            Supabase.Client supabase, Guid userId, Guid spaceId, CancellationToken ct);

        /// <summary>
        /// Ends a live space (host only — Forbidden otherwise). Stamps ended_at,
        /// marks every participant as left, and returns the space plus the
        /// participants that were publishing so their SFU tracks can be closed.
        /// </summary>
        Task<(Space Space, List<SpaceParticipant> Publishers)> EndSpace(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, CancellationToken ct);

        /// <summary>
        /// The caller's live participant row; throws NotFound for a dead space and
        /// Forbidden when the caller is not a live participant. Refreshes last_seen_at.
        /// </summary>
        Task<SpaceParticipant> GetLiveParticipant(
            Supabase.Client supabase, Guid spaceId, Guid userId, CancellationToken ct);

        /// <summary>
        /// Like <see cref="GetLiveParticipant"/> but additionally throws Forbidden
        /// unless the caller's persisted role is host or speaker *right now* — the
        /// publish-time role check of specs/audio-spaces.md.
        /// </summary>
        Task<SpaceParticipant> GetPublisherParticipant(
            Supabase.Client supabase, Guid spaceId, Guid userId, CancellationToken ct);

        /// <summary>Records the SFU session/track granted to a publisher.</summary>
        Task SetPublishState(
            Supabase.Client supabase, Guid spaceId, Guid userId,
            string sfuSessionId, string trackName, string sfuMid, CancellationToken ct);

        /// <summary>Sets the caller's raised-hand flag (participants only).</summary>
        Task SetHandRaised(
            Supabase.Client supabase, Guid spaceId, Guid userId, bool raised, CancellationToken ct);

        /// <summary>
        /// Host approves a raised hand: listener → speaker (clears the hand).
        /// Throws Forbidden unless the caller is the host, NotFound when the target
        /// is not a live participant. Returns the updated participant.
        /// </summary>
        Task<SpaceParticipant> ApproveSpeaker(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, Guid targetUserId, CancellationToken ct);

        /// <summary>
        /// Host demotes a speaker → listener and clears their SFU state. Returns the
        /// participant as it was before demotion so the published track can be closed.
        /// </summary>
        Task<SpaceParticipant> DemoteSpeaker(
            Supabase.Client supabase, Guid hostUserId, Guid spaceId, Guid targetUserId, CancellationToken ct);

        // ----- Reaper support (specs/audio-spaces.md §Implementation step 7) -----

        /// <summary>All spaces with no ended_at, oldest first (deterministic).</summary>
        Task<List<Space>> GetLiveSpaces(Supabase.Client supabase, CancellationToken ct);

        /// <summary>Live (not-left) participants of a space, deterministic order.</summary>
        Task<List<SpaceParticipant>> GetLiveParticipants(
            Supabase.Client supabase, Guid spaceId, CancellationToken ct);

        /// <summary>Marks a silently-dead participant as left and clears SFU state.</summary>
        Task MarkParticipantLeft(
            Supabase.Client supabase, SpaceParticipant participant, CancellationToken ct);

        /// <summary>System end (reaper): stamps ended_at without a host check.</summary>
        Task<Space> EndSpaceSystem(Supabase.Client supabase, Space space, CancellationToken ct);
    }
}
