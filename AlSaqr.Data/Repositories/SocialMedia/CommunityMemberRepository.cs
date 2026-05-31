using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
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
            Guid communityId)
        {
            // Upsert the membership row as a full member.
            var member = new CommunityMember
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                UserId = userId,
                Role = RoleMember,
                JoinedAt = DateTime.UtcNow,
            };

            await supabase
                .From<CommunityMember>()
                .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal });

            await CreateCommunityMemberNotification(
                supabase,
                userId: userId,
                communityId: communityId,
                messageTemplate: "{username} joined your community of {community}.",
                notificationType: "user_joined"
            );
        }

        public async Task UnJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId)
        {
            // Remove the membership row regardless of its role
            // (covers both JOINED and INVITED states from Neo4j).
            await supabase
                .From<CommunityMember>()
                .Where(cm => cm.UserId == userId && cm.CommunityId == communityId)
                .Delete();

            // Delete the "user_joined" notification on the founder's feed.
            var community = await supabase
                .From<Community>()
                .Where(c => c.Id == communityId)
                .Single();

            if (community != null)
            {
                await supabase
                    .From<Notification>()
                    .Where(n => n.UserId == community.FounderId
                                && n.CommunityId == communityId
                                && n.NotificationType == "user_joined")
                    .Delete();

                var unjoinedUser = await supabase
                        .From<AlSaqrUser>()
                        .Where(u => u.Id == userId)
                        .Single();

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: community.FounderId,
                    communityId: communityId,
                    messageTemplate: $"Someone with ID of {unjoinedUser?.Username} has unjoined your community of {community}.",
                    notificationType: "user_request_join"
                );
            }
        }

        public async Task RequestJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId)
        {
            var member = new CommunityMember
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                UserId = userId,
                Role = RoleRequested,
                JoinedAt = DateTime.UtcNow,
            };

            await supabase
                .From<CommunityMember>()
                .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal });

            await CreateCommunityMemberNotification(
                supabase,
                userId: userId,
                communityId: communityId,
                messageTemplate: "{username} has requested to join your community of {community}.",
                notificationType: "user_request_join"
            );
        }


        public async Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            bool accept)
        {
            if (accept)
            {
                // Promote the pending request row to an invited/member row.
                var existing = await supabase
                    .From<CommunityMember>()
                    .Where(cm => cm.UserId == userId
                                && cm.CommunityId == communityId)
                    .Filter("role", Operator.Equals, RoleRequested)
                    .Single();

                if (existing != null)
                {
                    existing.Role = RoleInvited;
                    existing.JoinedAt = DateTime.UtcNow;

                    await supabase
                        .From<CommunityMember>()
                        .Where(cm => cm.Id == existing.Id)
                        .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal });
                }
                else
                {
                    // No pending request found — create the invited row directly.
                    var member = new CommunityMember
                    {
                        Id = Guid.NewGuid(),
                        CommunityId = communityId,
                        UserId = userId,
                        Role = RoleInvited,
                        JoinedAt = DateTime.UtcNow,
                    };

                    await supabase
                        .From<CommunityMember>()
                        .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal });
                }

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: userId,
                    communityId: communityId,
                    messageTemplate: "{username} joined your community of {community}.",
                    notificationType: "user_joined"
                );
            }
            else
            {
                // Deny: remove the pending request row.
                await supabase
                    .From<CommunityMember>()
                    .Where(cm => cm.UserId == userId && cm.CommunityId == communityId)
                    .Filter("role", Operator.Equals, RoleRequested)
                    .Delete();

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: userId,
                    communityId: communityId,
                    messageTemplate: "{username} denied from your community of {community}.",
                    notificationType: "user_joined"
                );
            }

            // In both branches, delete the original "user_request_join" notification.
            var community = await supabase
                .From<Community>()
                .Where(c => c.Id == communityId)
                .Single();

            if (community != null)
            {
                await supabase
                    .From<Notification>()
                    .Where(n => n.UserId == community.FounderId
                                && n.CommunityId == communityId
                                && n.NotificationType == "user_request_join")
                    .Delete();
            }
        }


        private async Task CreateCommunityMemberNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            string messageTemplate,
            string notificationType)
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
