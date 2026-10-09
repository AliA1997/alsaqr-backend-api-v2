namespace AlSaqr.Infrastructure.Video
{
    /// <summary>A minted room token, where to connect, and when the token stops working.</summary>
    public sealed record SfuJoinToken(string Token, string WsUrl, string Group, DateTime ExpiresAt);

    /// <summary>
    /// SFU control plane for event video. The browser only ever receives a
    /// SfuJoinToken; admin credentials and the signing secret stay behind this
    /// interface. No recording, egress, or transcription operation exists here,
    /// and none may be added (C10).
    /// </summary>
    public interface IVideoSfuService
    {
        /// <summary>
        /// Creates the room with the cost guards: occupancy cap (C12), recording off
        /// (C10), and an access cutoff at notAfterUtc (C11, backing the reaper).
        /// </summary>
        Task CreateRoomAsync(string roomName, int maxParticipants, DateTime notAfterUtc, CancellationToken ct = default);

        /// <summary>
        /// A short-lived (C8), room-scoped token. canPublish is half the permission
        /// model; SetPublishAsync changes it on a live connection.
        /// </summary>
        SfuJoinToken MintJoinToken(string roomName, string identity, bool canPublish);

        /// <summary>Grants or revokes publishing on a live connection, server-side.</summary>
        Task SetPublishAsync(string roomName, string identity, bool canPublish, CancellationToken ct = default);

        /// <summary>Disconnects these identities. Best effort: someone already gone is not an error.</summary>
        Task RemoveParticipantsAsync(string roomName, IReadOnlyCollection<string> identities, CancellationToken ct = default);

        /// <summary>Disconnects everyone and deletes the room. Best effort for a room already gone.</summary>
        Task DeleteRoomAsync(string roomName, CancellationToken ct = default);
    }
}
