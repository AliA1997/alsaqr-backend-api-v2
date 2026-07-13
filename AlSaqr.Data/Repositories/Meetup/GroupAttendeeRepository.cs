using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class GroupAttendeeRepository : IGroupAttendeeRepository
    {
        private readonly IAttendeeRepository _attendeeRepository;

        public GroupAttendeeRepository(IAttendeeRepository attendeeRepository)
        {
            _attendeeRepository = attendeeRepository;
        }

        public async Task JoinGroup(
            Supabase.Client supabase,
            Guid userId,
            Guid groupId,
            CancellationToken ct)
        {
            try
            {
                var attendee = await _attendeeRepository.InsertOrRetrieveAttendeeForUser(
                    supabase,
                    userId,
                    ct);

                // Already on the group — nothing to do, and no second notification.
                var existingGroupAttendee = await FindGroupAttendee(supabase, groupId, attendee.Id, ct);

                if (existingGroupAttendee != null)
                    return;

                await InsertOrRetrieveGroupAttendee(supabase, groupId, attendee, false, ct);

                await CreateGroupAttendeeNotification(
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

        public async Task RemoveGroupAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid groupId,
            Guid attendeeUserId,
            CancellationToken ct)
        {
            try
            {
                // Check the acting user is the group founder, if he isn't return an exception.
                var group = await supabase
                                    .From<Groups>()
                                    .Filter("id", Operator.Equals, groupId.ToString())
                                    .Filter("founder_id", Operator.Equals, founderId.ToString())
                                    .Single(ct);

                if (group == null)
                    throw new Exception("Only the group founder can remove group attendees.");

                var attendee = await supabase
                    .From<Attendee>()
                    .Filter("user_id", Operator.Equals, attendeeUserId.ToString())
                    .Single(ct);

                // No attendee record means the user was never on the group.
                if (attendee == null)
                    return;

                await supabase
                    .From<GroupAttendees>()
                    .Where(ga => ga.AttendeeId == attendee.Id && ga.GroupId == groupId)
                    .Delete(null, ct);

                await CreateGroupAttendeeNotification(
                    supabase,
                    userId: attendeeUserId,
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

        public async Task<GroupAttendees> InsertOrRetrieveGroupAttendee(
            Supabase.Client supabase,
            Guid groupId,
            Attendee attendee,
            bool isGroupOrganizer = false,
            CancellationToken ct = default)
        {
            var existing = await FindGroupAttendee(supabase, groupId, attendee.Id, ct);

            if (existing != null)
                return existing;

            var groupAttendee = new GroupAttendees()
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                AttendeeId = attendee.Id,
                IsGroupOrganizer = isGroupOrganizer,
                CreatedAt = DateTime.UtcNow,
            };

            var inserted = await supabase
                .From<GroupAttendees>()
                .Insert(groupAttendee, new QueryOptions { Returning = ReturnType.Representation }, ct);

            return inserted.Models.FirstOrDefault()
                ?? throw new Exception($"Attendee {attendee.Id} could not be added to group {groupId}.");
        }

        public async Task InsertGroupAttendees(
            Supabase.Client supabase,
            Guid groupId,
            List<IDictionary<string, object>> groupAttendees,
            CancellationToken ct = default)
        {
            foreach (var groupAttendee in groupAttendees)
            {
                var attendee = await _attendeeRepository.InsertOrRetrieveAttendee(
                    supabase,
                    groupAttendee["name"].ToString(),
                    Guid.Parse(groupAttendee["user_id"].ToString())
                );

                await InsertOrRetrieveGroupAttendee(supabase, groupId, attendee, false, ct);
            }
        }

        private static async Task<GroupAttendees?> FindGroupAttendee(
            Supabase.Client supabase,
            Guid groupId,
            Guid attendeeId,
            CancellationToken ct)
            => await supabase
                .From<GroupAttendees>()
                .Filter("group_id", Operator.Equals, groupId.ToString())
                .Filter("attendee_id", Operator.Equals, attendeeId.ToString())
                .Single(ct);

        private static async Task CreateGroupAttendeeNotification(
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

            var groupAttendee = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var groupAttendeeName = groupAttendee?.Username ?? "Someone";

            var message = messageTemplate
                .Replace("{username}", groupAttendeeName)
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
                Link = $"/users/{groupAttendeeName}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Representation }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
