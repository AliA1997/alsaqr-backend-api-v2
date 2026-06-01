using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommunityMemberRepository: ICommunityMemberRepository
    {

        public CommunityMemberRepository() { }


        // Membership roles. The Neo4j relationship types (JOINED / INVITED /
        // INVITE_REQUESTED) collapse onto a single CommunityMember row that is
        // distinguished by its Role value.
        private const string RoleMember = "member";
        private const string RoleInvited = "invited";
        private const string RoleRequested = "requested";

        
        public async Task JoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct)
        {
            try
            {
                // Upsert the membership row as a full member.
                var member = new CommunityMember
                {
                    CommunityId = communityId,
                    UserId = userId,
                    Role = RoleMember,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<CommunityMember>()
                    .Insert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: userId,
                    communityId: communityId,
                    messageTemplate: "{username} joined your community of {community}.",
                    notificationType: "user_joined",
                    ct
                );

            }
            catch(JoinCommunityException ex) 
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new JoinCommunityException(communityId, ex);
            }
        }

        public async Task UnJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId, 
            CancellationToken ct)
        {
            try 
            {

                // Delete the "user_joined" notification on the founder's feed.
                var community = await supabase
                    .From<Community>()
                    .Where(c => c.Id == communityId)
                    .Single(ct);

                if (community != null)
                {
                    await supabase
                        .From<Notification>()
                        .Filter("user_id", Operator.Equals, userId)
                        .Filter("community_id", Operator.Equals, communityId)
                        .Filter("notification_type", Operator.Equals, "user_joined")
                        .Delete(null, ct);

                    var unjoinedUser = await supabase
                            .From<AlSaqrUser>()
                            .Where(u => u.Id == userId)
                            .Single(ct);

                    await CreateCommunityMemberNotification(
                        supabase,
                        userId: community.FounderId,
                        communityId: communityId,
                        messageTemplate: $"Someone with ID of {unjoinedUser?.Username} has unjoined your community of {community}.",
                        notificationType: "user_request_join",
                        ct
                    );
                }

                // Remove the membership row regardless of its role
                // (covers both JOINED and INVITED states from Neo4j).
                await supabase
                    .From<CommunityMember>()
                    .Where(cm => cm.UserId == userId && cm.CommunityId == communityId)
                    .Delete(null, ct);

            }
            catch(UnJoinCommunityException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new UnJoinCommunityException(communityId, ex);
            }
        }

        public async Task RequestJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct)
        {
            try 
            {
                var member = new CommunityMember
                {
                    CommunityId = communityId,
                    UserId = userId,
                    Role = RoleRequested,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<CommunityMember>()
                    .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: userId,
                    communityId: communityId,
                    messageTemplate: "{username} has requested to join your community of {community}.",
                    notificationType: "user_request_join",
                    ct
                );
            }
            catch(RequestToJoinCommunityException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new RequestToJoinCommunityException(communityId, ex);
            }

        }


        public async Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            bool accept, 
            CancellationToken ct)
        {
            try
            {
                if (accept)
                {
                    // Promote the pending request row to an invited/member row.
                    var existing = await supabase
                        .From<CommunityMember>()
                        .Where(cm => cm.UserId == userId
                                    && cm.CommunityId == communityId)
                        .Filter("role", Operator.Equals, RoleRequested)
                        .Single(ct);

                    if (existing != null)
                    {
                        existing.Role = RoleInvited;
                        existing.JoinedAt = DateTime.UtcNow;

                        await supabase
                            .From<CommunityMember>()
                            .Where(cm => cm.Id == existing.Id)
                            .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal }, ct);
                    }
                    else
                    {
                        // No pending request found — create the invited row directly.
                        var member = new CommunityMember
                        {
                            CommunityId = communityId,
                            UserId = userId,
                            Role = RoleInvited,
                            JoinedAt = DateTime.UtcNow,
                        };

                        await supabase
                            .From<CommunityMember>()
                            .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);
                    }

                    await CreateCommunityMemberNotification(
                        supabase,
                        userId: userId,
                        communityId: communityId,
                        messageTemplate: "{username} joined your community of {community}.",
                        notificationType: "user_joined",
                        ct
                    );
                }
                else
                {
                    // Deny: remove the pending request row.
                    await supabase
                        .From<CommunityMember>()
                        .Where(cm => cm.UserId == userId && cm.CommunityId == communityId)
                        .Filter("role", Operator.Equals, RoleRequested)
                        .Delete(null, ct);

                    await CreateCommunityMemberNotification(
                        supabase,
                        userId: userId,
                        communityId: communityId,
                        messageTemplate: "{username} denied from your community of {community}.",
                        notificationType: "user_joined",
                        ct
                    );
                }

                // In both branches, delete the original "user_request_join" notification.
                var community = await supabase
                    .From<Community>()
                    .Where(c => c.Id == communityId)
                    .Single(ct);

                if (community != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.UserId == community.FounderId
                                    && n.CommunityId == communityId
                                    && n.NotificationType == "user_request_join")
                        .Delete(null, ct);
                }

            }
            catch (RespondToRequestToJoinCommunityException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new RespondToRequestToJoinCommunityException(communityId, ex);
            }
        }


        private async Task CreateCommunityMemberNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            string messageTemplate,
            string notificationType, 
            CancellationToken ct = default)
        {
            var community = await supabase
                .From<Community>()
                .Where(c => c.Id == communityId)
                .Single();

            if (community == null || community.FounderId == userId)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single();

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{username}", username)
                .Replace("{community}", community.Name);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = community.FounderId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "community",
                CommunityId = communityId,
                Link = $"/communities/{communityId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Minimal });

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
