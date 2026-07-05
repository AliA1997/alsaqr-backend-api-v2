using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class GroupMemberRepository : IGroupMemberRepository
    {
        public GroupMemberRepository() { }

        private const string RoleMember = "member";

        public async Task JoinGroup(
            Supabase.Client supabase,
            Guid userId,
            Guid groupId,
            CancellationToken ct)
        {
            try
            {
                // Already a member — nothing to do (group_member is unique per user/group).
                var existingGroupMember = await supabase
                                        .From<GroupMember>()
                                        .Filter("user_id", Operator.Equals, userId.ToString())
                                        .Filter("group_id", Operator.Equals, groupId.ToString())
                                        .Single(ct);

                if (existingGroupMember != null)
                    return;

                var member = new GroupMember
                {
                    Id = Guid.NewGuid(),
                    GroupId = groupId,
                    UserId = userId,
                    Role = RoleMember,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<GroupMember>()
                    .Insert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateGroupMemberNotification(
                    supabase,
                    userId: userId,
                    groupId: groupId,
                    messageTemplate: "{username} joined your group of {group}.",
                    notificationType: "user_joined_group",
                    ct
                );
            }
            catch (JoinGroupException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new JoinGroupException(groupId, ex);
            }
        }

        public async Task RemoveGroupMember(
            Supabase.Client supabase,
            Guid founderId,
            Guid groupId,
            Guid memberUserId,
            CancellationToken ct)
        {
            try
            {
                // Check the acting user is the group founder, if he isn't return an exception.
                var group = (await supabase
                    .From<Groups>()
                    .Filter("id", Operator.Equals, groupId.ToString())
                    .Filter("founder_id", Operator.Equals, founderId.ToString())
                    .Get(ct)).Models.FirstOrDefault();

                if (group == null)
                    throw new Exception("Only the group founder can remove group members.");

                await supabase
                    .From<GroupMember>()
                    .Where(gm => gm.UserId == memberUserId && gm.GroupId == groupId)
                    .Delete(null, ct);

                await CreateGroupMemberNotification(
                    supabase,
                    userId: memberUserId,
                    groupId: groupId,
                    messageTemplate: "{username} was removed from your group of {group}.",
                    notificationType: "user_removed_group",
                    ct
                );
            }
            catch (UnjoinGroupException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new UnjoinGroupException(groupId, ex);
            }
        }

        private static async Task CreateGroupMemberNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid groupId,
            string messageTemplate,
            string notificationType,
            CancellationToken ct = default)
        {
            var group = await supabase
                .From<Groups>()
                .Where(g => g.Id == groupId)
                .Single(ct);

            if (group == null || group.FounderId == null || group.FounderId == userId)
                return;

            var groupMember = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var groupMemberName = groupMember?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{username}", groupMemberName)
                .Replace("{group}", group.Name ?? "your group");

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = group.FounderId.Value,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "group",
                RelatedUserId = userId,
                GroupId = groupId,
                Link = $"/users/{groupMemberName}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Representation }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
