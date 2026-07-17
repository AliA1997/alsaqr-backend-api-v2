using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class EventAttendeeRepository : IEventAttendeeRepository
    {
        private readonly IGroupAttendeeRepository _groupAttendeeRepository;
        private readonly IAttendeeRepository _attendeeRepository;

        public EventAttendeeRepository(
            IGroupAttendeeRepository groupAttendeeRepository,
            IAttendeeRepository attendeeRepository)
        {
            _groupAttendeeRepository = groupAttendeeRepository;
            _attendeeRepository = attendeeRepository;
        }

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

                // event_attendees.group_id is not nullable, so an event with no host group
                // cannot be attended.
                if (existingEvent.GroupId is not Guid groupId)
                    throw new Exception($"Event with ID: {eventId} has no host group.");

                // Joining an event also joins its group when the user isn't on it yet (spec rule).
                await _groupAttendeeRepository.JoinGroup(supabase, userId, groupId, ct);

                var attendee = await _attendeeRepository.InsertOrRetrieveAttendeeForUser(
                    supabase,
                    userId,
                    ct);

                // Already attending — nothing to do (one row per attendee/event).
                var existingEventAttendee = await supabase
                                                .From<EventAttendees>()
                                                .Where(ea => ea.AttendeeId == attendee.Id && ea.EventId == eventId)
                                                .Single(ct);

                if (existingEventAttendee != null)
                    return;

                var eventAttendee = new EventAttendees
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    AttendeeId = attendee.Id,
                    GroupId = groupId,
                    IsEventOrganizer = false,
                    CreatedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<EventAttendees>()
                    .Insert(eventAttendee, new QueryOptions { Returning = ReturnType.Minimal }, ct);
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

        public async Task LeaveEvent(
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

                // event_attendees.group_id is not nullable, so an event with no host group
                // cannot be attended.
                if (existingEvent.GroupId is not Guid groupId)
                    throw new Exception($"Event with ID: {eventId} has no host group.");


                var attendee = await _attendeeRepository.InsertOrRetrieveAttendeeForUser(
                    supabase,
                    userId,
                    ct);

                // Already attending — nothing to do (one row per attendee/event).
                var existingEventAttendee = await supabase
                                                .From<EventAttendees>()
                                                .Where(ea => ea.AttendeeId == attendee.Id && ea.EventId == eventId)
                                                .Single(ct);

                if (existingEventAttendee == null)
                    return;


                await supabase
                    .From<EventAttendees>()
                    .Delete(existingEventAttendee, new QueryOptions { Returning = ReturnType.Minimal }, ct);
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


        public async Task RemoveEventAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid attendeeUserId,
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
                var group = await supabase
                                    .From<Groups>()
                                    .Filter("id", Operator.Equals, eventToUpdate.GroupId.ToString())
                                    .Filter("founder_id", Operator.Equals, founderId.ToString())
                                    .Single(ct);

                if (group == null)
                    throw new Exception("Only the group founder can remove event attendees.");

                var attendee = await supabase
                    .From<Attendee>()
                    .Filter("user_id", Operator.Equals, attendeeUserId.ToString())
                    .Single(ct);

                // No attendee record means the user was never attending anything.
                if (attendee == null)
                    return;

                // Only the event attendance is removed — the user stays in the group (spec rule).
                await supabase
                    .From<EventAttendees>()
                    .Where(ea => ea.AttendeeId == attendee.Id && ea.EventId == eventId)
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
