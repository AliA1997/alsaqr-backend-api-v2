namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// Settings for publishing authoritative realtime events on space channels,
    /// bound from the "Supabase" configuration section. Uses the service-role
    /// key so backend-emitted events (role_changed, space_ended, track_added,
    /// track_closed) are trusted; the key must never reach a client
    /// (specs/audio-spaces.md).
    /// </summary>
    public sealed class SupabaseRealtimeConfig
    {
        public string Url { get; set; } = default!;
        public string ServiceRoleSecret { get; set; } = default!;
    }
}
