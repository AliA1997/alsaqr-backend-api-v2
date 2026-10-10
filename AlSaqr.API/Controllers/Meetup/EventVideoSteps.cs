using AlSaqr.Infrastructure.Video;

namespace AlSaqr.API.Controllers.Meetup
{
    /// <summary>
    /// SFU and broadcast steps shared by the actions that change who is live on an
    /// event's video: EventVideoController, and attendee removal in EventsController.
    /// </summary>
    internal static class EventVideoSteps
    {
        /// <summary>Disconnects one identity. Already gone is the outcome wanted, not an error.</summary>
        public static async Task RemoveFromRoom(
            IVideoSfuService sfu, string roomName, string identity, CancellationToken ct)
        {
            try
            {
                await sfu.RemoveParticipantsAsync(roomName, new[] { identity }, ct);
            }
            catch (ParticipantNotConnectedException)
            {
                // Already gone: that is the outcome we wanted.
            }
        }

        /// <summary>
        /// Broadcasts are notifications sent after everything else succeeded: a failure
        /// is logged and never turns a completed action into an error.
        /// </summary>
        public static async Task BroadcastSafely(ILogger logger, Func<Task> broadcast, string name, Guid eventId)
        {
            try
            {
                await broadcast();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Broadcasting {Name} for event {EventId} failed.", name, eventId);
            }
        }
    }
}
