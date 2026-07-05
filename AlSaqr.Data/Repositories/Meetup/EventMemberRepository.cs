using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class EventMemberRepository : IEventMemberRepository
    {
        private readonly IGroupMemberRepository _groupMemberRepository;

        public EventMemberRepository(IGroupMemberRepository groupMemberRepository)
        {
            _groupMemberRepository = groupMemberRepository;
        }

        private const string RoleMember = "member";

        public async Task JoinEvent(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            CancellationToken ct)
        {
            try
            {
                var existingEvent = await supabase
                                    .From<Event>()
                                    .Filter("id", Operator.Equals, eventId.ToString())
                                    .Single(ct);

                if (existingEvent == null)
                    throw new Exception($"Event with ID: {eventId} not found.");

                // Joining an event also joins its group when the user isn't a member yet (spec rule).
                if (existingEvent.GroupId is Guid groupId)
                    await _groupMemberRepository.JoinGroup(supabase, userId, groupId, ct);

                // Already attending — nothing to do (event_member is unique per user/event).
                var existingEventMember = (await supabase
                    .From<EventMember>()
                    .Where(em => em.UserId == userId && em.EventId == eventId)
                    .Get(ct)).Models.FirstOrDefault();

                if (existingEventMember != null)
                    return;

                var member = new EventMember
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    UserId = userId,
                    Role = RoleMember,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<EventMember>()
                    .Insert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);
            }
            catch (JoinEventException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new JoinEventException(eventId, ex);
            }
        }

        public async Task RemoveEventMember(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid memberUserId,
            CancellationToken ct)
        {
            try
            {
                var eventToUpdate = await supabase
                    .From<Event>()
                    .Where(e => e.Id == eventId)
                    .Single(ct);

                if (eventToUpdate == null || eventToUpdate.GroupId == null)
                    throw new Exception($"Event with ID: {eventId} not found.");

                // Check the acting user is the founder of the event's group, if he isn't return an exception.
                var group = (await supabase
                    .From<Groups>()
                    .Filter("id", Operator.Equals, eventToUpdate.GroupId.ToString())
                    .Filter("founder_id", Operator.Equals, founderId.ToString())
                    .Get(ct)).Models.FirstOrDefault();

                if (group == null)
                    throw new Exception("Only the group founder can remove event members.");

                // Only the event membership is removed — the user stays in the group (spec rule).
                await supabase
                    .From<EventMember>()
                    .Where(em => em.UserId == memberUserId && em.EventId == eventId)
                    .Delete(null, ct);
            }
            catch (UnjoinEventException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new UnjoinEventException(eventId, ex);
            }
        }
    }
}
