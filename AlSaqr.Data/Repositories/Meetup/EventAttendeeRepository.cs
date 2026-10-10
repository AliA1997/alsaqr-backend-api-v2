using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Common;
using AlSaqr.Domain.Meetup.Exceptions;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class EventAttendeeRepository : IEventAttendeeRepository
    {
        private readonly IGroupAttendeeRepository _groupAttendeeRepository;
        private readonly IAttendeeRepository _attendeeRepository;
        private readonly IVideoStreamRepository _videoStreamRepository;

        public EventAttendeeRepository(
            IGroupAttendeeRepository groupAttendeeRepository,
            IAttendeeRepository attendeeRepository,
            IVideoStreamRepository videoStreamRepository)
        {
            _groupAttendeeRepository = groupAttendeeRepository;
            _attendeeRepository = attendeeRepository;
            _videoStreamRepository = videoStreamRepository;
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
            catch (JoinEventException)
            {
                throw;
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
            catch (UnjoinEventException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UnjoinEventException(eventId, ex);
            }
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)?> AuthorizeRemoveEventAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid attendeeUserId,
            CancellationToken ct)
        {
            var hostEvent = await RequireFoundedEvent(supabase, founderId, eventId, ct);
            return await LiveVideoStep(
                hostEvent,
                () => _videoStreamRepository.FindLiveParticipant(supabase, attendeeUserId, eventId, ct));
        }

        public async Task<bool> RemoveEventAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid attendeeUserId,
            CancellationToken ct)
        {
            try
            {
                var hostEvent = await RequireFoundedEvent(supabase, founderId, eventId, ct);

                // Kick first, whatever the attendance state: a failed kick throws before any
                // removal (the call stays retryable), and a live participant is cut off even
                // when the user has no Attendee record.
                var kicked = await LiveVideoStep(
                    hostEvent,
                    () => _videoStreamRepository.KickParticipant(supabase, attendeeUserId, eventId, ct));

                var attendee = await supabase
                    .From<Attendee>()
                    .Filter("user_id", Operator.Equals, attendeeUserId.ToString())
                    .Single(ct);

                // No attendee record means the user was never attending anything.
                if (attendee == null)
                    return kicked != null;

                // Only the event attendance is removed — the user stays in the group (spec rule).
                await supabase
                    .From<EventAttendees>()
                    .Where(ea => ea.AttendeeId == attendee.Id && ea.EventId == eventId)
                    .Delete(null, ct);

                return kicked != null;
            }
            catch (Exception ex) when (ex is not (NotFoundException or ForbiddenException))
            {
                throw new UnjoinEventException(eventId, ex);
            }
        }

        /// <summary>The event, once the caller is confirmed as the founder of its group.</summary>
        private static async Task<Event> RequireFoundedEvent(
            Supabase.Client supabase, Guid founderId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, eventId.ToString())
                .Single(ct);

            if (hostEvent?.GroupId is not Guid groupId)
                throw new NotFoundException($"Event with ID: {eventId} not found.");

            var group = await supabase
                .From<Groups>()
                .Filter("id", Operator.Equals, groupId.ToString())
                .Filter("founder_id", Operator.Equals, founderId.ToString())
                .Single(ct);

            if (group == null)
                throw new ForbiddenException("Only the group founder can remove event attendees.");

            return hostEvent;
        }

        /// <summary>
        /// Runs a video step of attendee removal only where video can be live: the event
        /// is online, and the video tables are deployed (a deployment without them has
        /// nothing live). Null otherwise. Any other error propagates.
        /// </summary>
        private static async Task<(VideoStream Stream, VideoParticipant Participant)?> LiveVideoStep(
            Event hostEvent, Func<Task<(VideoStream Stream, VideoParticipant Participant)?>> step)
        {
            if (!hostEvent.IsOnline)
                return null;

            try
            {
                return await step();
            }
            catch (PostgrestException ex) when (PostgrestErrors.IsMissingTable(ex))
            {
                return null;
            }
        }
    }
}
