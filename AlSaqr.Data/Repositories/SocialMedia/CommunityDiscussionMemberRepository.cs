using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
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
            Guid communityDiscussionId)
        {
            // Upsert the membership row as a full member.
            var member = new CommunityDiscussionMember
            {
                Id = Guid.NewGuid(),
                CommunityDiscussionId = communityDiscussionId,
                UserId = userId,
                Role = RoleMember,
                JoinedAt = DateTime.UtcNow,
            };

            await supabase
                .From<CommunityDiscussionMember>()
                .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal });

            await CreateCommunityDiscussionMemberNotification(
                supabase,
                userId: userId,
                communityDiscussionId: communityDiscussionId,
                messageTemplate: "{username} joined your community discussion of {communityDiscussion}.",
                notificationType: "user_joined"
            );
        }

        public async Task UnJoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId)
        {
            // Remove the membership row regardless of its role
            // (covers both JOINED and INVITED states from Neo4j).
            await supabase
                .From<CommunityDiscussionMember>()
                .Where(cm => cm.UserId == userId && cm.CommunityDiscussionId == communityDiscussionId)
                .Delete();

            // Delete the "user_joined" notification on the founder's feed.
            var communityDiscussion = await supabase
                .From<CommunityDiscussion>()
                .Where(c => c.Id == communityDiscussionId)
                .Single();

            if (communityDiscussion != null)
            {
                await supabase
                    .From<Notification>()
                    .Where(n => n.UserId == communityDiscussion.CreatorId
                                && n.CommunityDiscussionId == communityDiscussionId
                                && n.NotificationType == "user_joined")
                    .Delete();

                var unjoinedUser = await supabase
                        .From<AlSaqrUser>()
                        .Where(u => u.Id == userId)
                        .Single();

                await CreateCommunityDiscussionMemberNotification(
                    supabase,
                    userId: communityDiscussion.CreatorId,
                    communityDiscussionId: communityDiscussionId,
                    messageTemplate: $"Someone with ID of {unjoinedUser?.Username} has unjoined your community discussion of {communityDiscussion.Title}.",
                    notificationType: "user_request_join"
                );
            }
        }

        public async Task RequestJoinCommunityDiscussion(
              Supabase.Client supabase,
              Guid userId,
              Guid communityDiscussionId)
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
                .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal });

            await CreateCommunityDiscussionMemberNotification(
                supabase,
                userId: userId,
                communityDiscussionId: communityDiscussionId,
                messageTemplate: "{username} has requested to join your community discussion of {communityDiscussion}.",
                notificationType: "user_request_join"
            );
        }


        public async Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            bool accept)
        {
            if (accept)
            {
                // Promote the pending request row to an invited/member row.
                var existing = await supabase
                    .From<CommunityDiscussionMember>()
                    .Where(cdm => cdm.UserId == userId
                                && cdm.CommunityDiscussionId == communityDiscussionId)
                    .Filter("role", Operator.Equals, RoleRequested)
                    .Single();

                if (existing != null)
                {
                    existing.Role = RoleInvited;
                    existing.JoinedAt = DateTime.UtcNow;

                    await supabase
                        .From<CommunityDiscussionMember>()
                        .Where(cdm => cdm.Id == existing.Id)
                        .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal });
                }
                else
                {
                    // No pending request found — create the invited row directly.
                    var member = new CommunityDiscussionMember
                    {
                        Id = Guid.NewGuid(),
                        CommunityDiscussionId = communityDiscussionId   ,
                        UserId = userId,
                        Role = RoleInvited,
                        JoinedAt = DateTime.UtcNow,
                    };

                    await supabase
                        .From<CommunityDiscussionMember>()
                        .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal });
                }

                await CreateCommunityDiscussionMemberNotification(
                    supabase,
                    userId: userId,
                    communityDiscussionId: communityDiscussionId,
                    messageTemplate: "{username} joined your community discussion of {communityDiscussion}.",
                    notificationType: "user_joined"
                );
            }
            else
            {
                // Deny: remove the pending request row.
                await supabase
                    .From<CommunityDiscussionMember>()
                    .Where(cdm => cdm.UserId == userId && cdm.CommunityDiscussionId == communityDiscussionId)
                    .Filter("role", Operator.Equals, RoleRequested)
                    .Delete();

                await CreateCommunityDiscussionMemberNotification(
                    supabase,
                    userId: userId,
                    communityDiscussionId: communityDiscussionId,
                    messageTemplate: "{username} denied from your community discussion of {communityDiscussion}.",
                    notificationType: "user_joined"
                );
            }

            // In both branches, delete the original "user_request_join" notification.
            var communityDiscussion = await supabase
                .From<CommunityDiscussion>()
                .Where(c => c.Id == communityDiscussionId)
                .Single();

            if (communityDiscussion != null)
            {
                await supabase
                    .From<Notification>()
                    .Where(n => n.UserId == communityDiscussion.CreatorId
                                && n.CommunityDiscussionId == communityDiscussionId
                                && n.NotificationType == "user_request_join")
                    .Delete();
            }
        }


        private async Task CreateCommunityDiscussionMemberNotification(
             Supabase.Client supabase,
             Guid userId,
             Guid communityDiscussionId,
             string messageTemplate,
             string notificationType)
        {
            var communityDiscussion = await supabase
                .From<CommunityDiscussion>()
                .Where(c => c.Id == communityDiscussionId)
                .Single();

            if (communityDiscussion == null || communityDiscussion.CreatorId == userId)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single();

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{username}", username)
                .Replace("{communityDiscussion}", communityDiscussion.Title);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = communityDiscussion.CreatorId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "community",
                CommunityDiscussionId = communityDiscussionId,
                Link = $"/communityDiscussions/{communityDiscussionId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Minimal });

            if (created == null)
                throw new Exception("Error creating notification");
        }

    }
}
