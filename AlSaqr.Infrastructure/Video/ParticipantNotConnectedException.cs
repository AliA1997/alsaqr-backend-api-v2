namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// A demote matched no live connection, so nothing was revoked. The caller decides
    /// whether that is fine (the person already left) or needs a retry.
    /// </summary>
    public sealed class ParticipantNotConnectedException : Exception
    {
        public ParticipantNotConnectedException(string roomName)
            : base($"No live connection matched the participant in room {roomName}.")
        {
        }
    }
}
