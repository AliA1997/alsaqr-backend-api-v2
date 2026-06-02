using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommunityDiscussionMemberRepository : ICommunityDiscussionMemberRepository
    {
        public CommunityDiscussionMemberRepository() { }

        // Membership roles. The Neo4j relationship types (JOINED / INVITED /
        // INVITE_REQUESTED) collapse onto a single CommunityMember row that is
        // distinguished by its Role value.
        private const string RoleMember = "member";
        private const string RoleInvited = "invited";
        private const string RoleRequested = "requested";

        public async Task JoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            CancellationToken ct)
        {
            try 
            {
                // Upsert the membership row as a full member.
                var member = new CommunityDiscussionMember
                {
                    CommunityDiscussionId = communityDiscussionId,
                    UserId = userId,
                    Role = RoleMember,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<CommunityDiscussionMember>()
                    .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateCommunityDiscussionMemberNotification(
                    supabase,
                    userId: userId,
                    communityDiscussionId: communityDiscussionId,
                    messageTemplate: "{username} joined your community discussion of {communityDiscussion}.",
                    notificationType: "user_joined",
                    ct
                );
            }
            catch (JoinCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new JoinCommunityDiscussionException(communityDiscussionId, ex);
            }
        }

        public async Task UnJoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            CancellationToken ct)
        {
            try
            {

                // Delete the "user_joined" notification on the founder's feed.
                var communityDiscussion = await supabase
                    .From<CommunityDiscussion>()
                    .Where(c => c.Id == communityDiscussionId)
                    .Single(ct);

                if (communityDiscussion != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.RelatedUserId == userId)
                        .Where(n => n.CommunityDiscussionId == communityDiscussionId)
                        .Delete(null, ct);

                    var unjoinedUser = await supabase
                            .From<AlSaqrUser>()
                            .Where(u => u.Id == userId)
                            .Single(ct);

                    await CreateCommunityDiscussionMemberNotification(
                        supabase,
                        userId: userId,
                        communityDiscussionId: communityDiscussionId,
                        messageTemplate: $"Someone with ID of {unjoinedUser?.Username} has unjoined your community discussion of {communityDiscussion.Title}.",
                        notificationType: "user_unjoined",
                        ct
                    );
                }

                await supabase
                    .From<CommunityDiscussionMember>()
                    .Where(cm => cm.UserId == userId && cm.CommunityDiscussionId == communityDiscussionId)
                    .Delete(null, ct);

            }
            catch(UnJoinCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new UnJoinCommunityDiscussionException(communityDiscussionId, ex);
            }
            
        }

        public async Task RequestJoinCommunityDiscussion(
              Supabase.Client supabase,
              Guid userId,
              Guid communityDiscussionId,
              CancellationToken ct)
        {
            try
            {
                var member = new CommunityDiscussionMember
                {
                    Id = Guid.NewGuid(),
                    CommunityDiscussionId = communityDiscussionId,
                    UserId = userId,
                    Role = RoleRequested,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<CommunityDiscussionMember>()
                    .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateCommunityDiscussionMemberNotification(
                    supabase,
                    userId: userId,
                    communityDiscussionId: communityDiscussionId,
                    messageTemplate: "{username} has requested to join your community discussion of {communityDiscussion}.",
                    notificationType: "user_request_join",
                    ct
                );
            }
            catch(RequestToJoinCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new RequestToJoinCommunityDiscussionException(communityDiscussionId, ex);
            }

        }


        public async Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            bool accept,
            CancellationToken ct)
        {
            try 
            {
                if (accept)
                {
                    // Promote the pending request row to an invited/member row.
                    var existing = await supabase
                        .From<CommunityDiscussionMember>()
                        .Where(cdm => cdm.UserId == userId)
                        .Where(cdm => cdm.CommunityDiscussionId == communityDiscussionId)
                        .Where(cdm => cdm.Role == RoleRequested)
                        .Single(ct);

                    if (existing != null)
                    {
                        existing.Role = RoleInvited;
                        existing.JoinedAt = DateTime.UtcNow;

                        await supabase
                            .From<CommunityDiscussionMember>()
                            .Where(cdm => cdm.Id == existing.Id)
                            .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal }, ct);
                    }
                    else
                    {
                        // No pending request found — create the invited row directly.
                        var member = new CommunityDiscussionMember
                        {
                            Id = Guid.NewGuid(),
                            CommunityDiscussionId = communityDiscussionId,
                            UserId = userId,
                            Role = RoleInvited,
                            JoinedAt = DateTime.UtcNow,
                        };

                        await supabase
                            .From<CommunityDiscussionMember>()
                            .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);
                    }

                    await CreateCommunityDiscussionMemberNotification(
                        supabase,
                        userId: userId,
                        communityDiscussionId: communityDiscussionId,
                        messageTemplate: "{username} invited to a community discussion of {communityDiscussion}.",
                        notificationType: "user_joined",
                        ct
                    );
                }
                else
                {
                    // Deny: remove the pending request row.
                    await supabase
                        .From<CommunityDiscussionMember>()
                        .Where(cdm => cdm.UserId == userId)
                        .Where(cdm => cdm.CommunityDiscussionId == communityDiscussionId)
                        .Where(cdm => cdm.Role == RoleRequested)
                        .Delete(null, ct);

                    await CreateCommunityDiscussionMemberNotification(
                        supabase,
                        userId: userId,
                        communityDiscussionId: communityDiscussionId,
                        messageTemplate: "{username} denied from your community discussion of {communityDiscussion}.",
                        notificationType: "user_denied",
                        ct
                    );
                }

                // In both branches, delete the original "user_request_join" notification.
                var communityDiscussion = await supabase
                    .From<CommunityDiscussion>()
                    .Where(c => c.Id == communityDiscussionId)
                    .Single(ct);

                if (communityDiscussion != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.UserId == communityDiscussion.CreatorId)
                        .Where(n => n.CommunityDiscussionId == communityDiscussionId)
                        .Where(n => n.NotificationType == "user_request_join")
                        .Delete(null, ct);
                }
            }
            catch(RespondToRequestToJoinCommunityDiscussionException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new RespondToRequestToJoinCommunityDiscussionException(communityDiscussionId, ex);
            }

        }


        private async Task CreateCommunityDiscussionMemberNotification(
             Supabase.Client supabase,
             Guid userId,
             Guid communityDiscussionId,
             string messageTemplate,
             string notificationType,
            CancellationToken ct)
        {
            var communityDiscussion = await supabase
                .From<CommunityDiscussion>()
                .Where(c => c.Id == communityDiscussionId)
                .Single(ct);

            if (communityDiscussion == null || communityDiscussion.CreatorId == userId)
                return;

            var communityDiscussionMember = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var communityDiscussionMemberUsername = communityDiscussionMember?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{username}", communityDiscussionMemberUsername)
                .Replace("{communityDiscussion}", communityDiscussion.Title);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = communityDiscussion.CreatorId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,  
                NotificationType = notificationType,
                ItemType = "community_discussion",
                CommunityDiscussionId = communityDiscussionId,
                RelatedUserId = userId,
                Link = $"/users/{communityDiscussionMemberUsername}"
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Representation }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }

    }
}
