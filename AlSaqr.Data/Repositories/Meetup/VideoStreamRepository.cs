using System.Globalization;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Common;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using static AlSaqr.Domain.Meetup.EventVideo;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    /// <summary>
    /// Video streams for online events (specs/video-streaming-online-events.md §6).
    /// The backend is authoritative for attendance and roles: every gate is checked
    /// here, before the controller makes any SFU call or mints any token.
    /// </summary>
    public class VideoStreamRepository : IVideoStreamRepository
    {
        public async Task<VideoStreamToDisplay?> GetLiveEventStream(
            Supabase.Client supabase, Guid eventId, CancellationToken ct)
        {
            var stream = await FindLiveStream(supabase, eventId, ct);
            return stream == null ? null : await ToDisplay(supabase, stream, ct);
        }

        public async Task<VideoStreamToDisplay> RequireLiveEventStream(
            Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            await ToDisplay(supabase, await GetLiveStreamOrThrow(supabase, eventId, ct), ct);

        /// <summary>
        /// The pre-check is an optimisation; the partial unique index is the guard. Two
        /// concurrent starts both pass the pre-check and PostgREST returns 23505 for the
        /// loser, which must surface as 409, not 500.
        /// </summary>
        public async Task<VideoStream> StartEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);

            if (!await CanControlEventVideo(supabase, hostEvent, userId, ct))
                throw new ForbiddenException("Only the event organizer or group founder may start the video stream.");

            if (await FindLiveStream(supabase, eventId, ct) != null)
                throw new ConflictException("This event already has a live video stream.");

            var streamId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            try
            {
                var created = (await supabase
                    .From<VideoStream>()
                    .Insert(
                        new VideoStream
                        {
                            Id = streamId,
                            EventId = eventId,
                            RoomName = VideoStream.RoomNameFor(streamId),   // stream-scoped (§4)
                            HostId = userId,
                            StartedAt = now,
                        },
                        new QueryOptions { Returning = ReturnType.Representation },
                        ct))
                    .Models.FirstOrDefault()
                    ?? throw new ConflictException("The video stream could not be created.");

                await supabase
                    .From<VideoParticipant>()
                    .Insert(
                        new VideoParticipant
                        {
                            Id = Guid.NewGuid(),
                            EventId = eventId,
                            ParticipantId = userId,
                            VideoStreamId = created.Id,
                            Role = VideoParticipant.RoleHost,
                            // No SFU identity until the host actually joins: it marks "has a
                            // token and may be connected", which drives second-device kicks.
                            JoinedAt = now,
                            LastSeenAt = now,
                        },
                        new QueryOptions { Returning = ReturnType.Minimal },
                        ct);

                return created;
            }
            catch (PostgrestException ex) when (IsUniqueViolation(ex))
            {
                // Lost the race against a concurrent start.
                throw new ConflictException("This event already has a live video stream.");
            }
        }

        public Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct) =>
            EndStream(supabase, stream, VideoParticipant.LeftEnded, ct);

        public async Task<List<VideoParticipant>> GetLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Order("joined_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task MarkParticipantLeft(
            Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct)
        {
            participant.LeftAt = DateTime.UtcNow;
            participant.LeftReason = reason;
            participant.SfuIdentity = null;

            await supabase
                .From<VideoParticipant>()
                .Upsert(participant, new QueryOptions { Returning = ReturnType.Minimal }, ct);
        }

        // ----- stream lifecycle -----

        private async Task<VideoStream> EndStream(
            Supabase.Client supabase, VideoStream stream, string reason, CancellationToken ct)
        {
            foreach (var participant in await GetLiveParticipants(supabase, stream.Id, ct))
                await MarkParticipantLeft(supabase, participant, reason, ct);

            stream.EndedAt = DateTime.UtcNow;

            return (await supabase
                    .From<VideoStream>()
                    .Upsert(stream, new QueryOptions { Returning = ReturnType.Representation }, ct))
                .Models.First();
        }

        private static Task<VideoStream?> FindLiveStream(Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            supabase
                .From<VideoStream>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

        private static async Task<VideoStream> GetLiveStreamOrThrow(
            Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            await FindLiveStream(supabase, eventId, ct)
            ?? throw new NotFoundException("No live video stream for this event.");

        private static async Task<VideoStreamToDisplay> ToDisplay(
            Supabase.Client supabase, VideoStream stream, CancellationToken ct)
        {
            var hostEvent = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, stream.EventId.ToString())
                .Single(ct);

            var host = await supabase
                .From<AlSaqrUser>()
                .Filter("id", Operator.Equals, stream.HostId.ToString())
                .Single(ct);

            var participantCount = await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Count(Constants.CountType.Exact, ct);

            return new VideoStreamToDisplay
            {
                VideoStreamId = stream.Id,
                EventId = stream.EventId,
                EventName = hostEvent?.Name,
                HostId = stream.HostId,
                HostUsername = host?.Username,
                HostAvatar = host?.Avatar,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                ParticipantCount = participantCount,
                IsLive = stream.EndedAt == null,
            };
        }

        // ----- authorization gates (spec §6.1) -----

        // C1: online events only, checked before any SFU call. A groupless event has
        // no attendees (event_attendees.group_id is NOT NULL), so no one to authorize.
        private static async Task<Event> GetOnlineEventOrThrow(
            Supabase.Client supabase, Guid eventId, CancellationToken ct)
        {
            var existing = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, eventId.ToString())
                .Single(ct)
                ?? throw new NotFoundException($"Event {eventId} does not exist.");

            if (!existing.IsOnline)
                throw new ValidationException("Video streaming is available for online events only.");

            if (existing.GroupId is null)
                throw new ValidationException("This event has no host group and cannot host video.");

            return existing;
        }

        // Attendance lives on event_attendees, keyed on attendees.id.
        private static async Task<bool> IsEventAttendee(
            Supabase.Client supabase, Guid eventId, Guid userId, CancellationToken ct)
        {
            var attendee = await supabase
                .From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (attendee == null)
                return false;

            var attending = await supabase
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Single(ct);

            return attending != null;
        }

        // Two legitimate authorities: the host group's founder (checked first, the
        // escalation path) and the event organizer.
        private static async Task<bool> CanControlEventVideo(
            Supabase.Client supabase, Event hostEvent, Guid userId, CancellationToken ct)
        {
            var founded = await supabase
                .From<Groups>()
                .Filter("id", Operator.Equals, hostEvent.GroupId!.Value.ToString())
                .Filter("founder_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (founded != null)
                return true;

            var attendee = await supabase
                .From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (attendee == null)
                return false;

            var organizer = await supabase
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, hostEvent.Id.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Filter("is_event_organizer", Operator.Equals, "true")
                .Single(ct);

            return organizer != null;
        }

        /// <summary>PostgreSQL 23505 — unique_violation. Content is PostgREST's JSON error body.</summary>
        private static bool IsUniqueViolation(PostgrestException ex) =>
            ex.Content?.Contains("23505", StringComparison.Ordinal) == true;

        /// <summary>UTC ISO-8601 with microseconds, for PostgREST timestamp filters.</summary>
        private static string ToUtcIso(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    }
}
