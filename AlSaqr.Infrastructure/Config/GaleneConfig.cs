namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// Galène (self-hosted, MIT SFU) settings, bound from the "Galene" configuration
    /// section. AdminPassword and TokenSecret are server-side only: they must never
    /// appear in a response, log line, or client-visible error
    /// (docs/superpowers/plans/2026-10-04-event-video-streaming.md).
    /// </summary>
    public sealed class GaleneConfig
    {
        /// <summary>Server-side base for the admin API and operator WebSocket, e.g. http://galene.internal:8443</summary>
        public string AdminUrl { get; set; } = "http://localhost:8443";

        /// <summary>Public origin browsers connect to; also the token audience host, e.g. https://video.alsaqr.app</summary>
        public string PublicUrl { get; set; } = "http://localhost:8443";

        public string AdminUsername { get; set; } = default!;

        public string AdminPassword { get; set; } = default!;

        /// <summary>HS256 room-token secret: exactly 32 bytes (Galène rejects any other HS256 key length), installed into every room's keys.</summary>
        public string TokenSecret { get; set; } = default!;

        /// <summary>Total room occupancy cap (C12) — Galène max-clients.</summary>
        public int MaxParticipants { get; set; } = 50;

        /// <summary>Concurrent publisher cap, host included (C5).</summary>
        public int MaxPresenters { get; set; } = 9;
    }
}
