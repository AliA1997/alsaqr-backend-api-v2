using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Spaces
{
    /// <summary>
    /// Publishes the backend-authoritative events of specs/audio-spaces.md on the
    /// Supabase realtime channel <c>space:{spaceId}</c> using the service-role
    /// key. Client-emitted events (mute_changed, hand_raised) are NOT re-emitted
    /// here. Presence is Supabase, media is Cloudflare — this service only ever
    /// touches the Supabase side.
    /// </summary>
    public interface ISpaceEventBroadcaster
    {
        /// <summary>After approve/demote; the promoted client publishes on receipt.</summary>
        Task RoleChangedAsync(Guid spaceId, Guid userId, string role, CancellationToken ct = default);

        /// <summary>After the host ends the space (or the reaper ends an empty one).</summary>
        Task SpaceEndedAsync(Guid spaceId, DateTime endedAt, CancellationToken ct = default);

        /// <summary>After a successful publish, so listeners pull the new track.</summary>
        Task TrackAddedAsync(Guid spaceId, Guid userId, string sfuSessionId, string trackName, CancellationToken ct = default);

        /// <summary>After leave/demote/reap unpublishes a track.</summary>
        Task TrackClosedAsync(Guid spaceId, Guid userId, CancellationToken ct = default);
    }

    public sealed class SpaceEventBroadcaster : ISpaceEventBroadcaster
    {
        private readonly HttpClient _httpClient;
        private readonly SupabaseRealtimeConfig _config;

        public SpaceEventBroadcaster(HttpClient httpClient, IOptions<SupabaseRealtimeConfig> config)
        {
            _httpClient = httpClient;
            _config = config.Value;
        }

        public Task RoleChangedAsync(Guid spaceId, Guid userId, string role, CancellationToken ct = default) =>
            BroadcastAsync(spaceId, "role_changed", new { userId, role }, ct);

        public Task SpaceEndedAsync(Guid spaceId, DateTime endedAt, CancellationToken ct = default) =>
            BroadcastAsync(spaceId, "space_ended", new { spaceId, endedAt }, ct);

        public Task TrackAddedAsync(Guid spaceId, Guid userId, string sfuSessionId, string trackName, CancellationToken ct = default) =>
            BroadcastAsync(spaceId, "track_added", new { userId, sfuSessionId, trackName }, ct);

        public Task TrackClosedAsync(Guid spaceId, Guid userId, CancellationToken ct = default) =>
            BroadcastAsync(spaceId, "track_closed", new { userId }, ct);

        private async Task BroadcastAsync(Guid spaceId, string eventName, object payload, CancellationToken ct)
        {
            var url = $"{_config.Url.TrimEnd('/')}/realtime/v1/api/broadcast";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("apikey", _config.ServiceRoleSecret);
            request.Headers.Add("Authorization", $"Bearer {_config.ServiceRoleSecret}");
            request.Content = JsonContent.Create(new BroadcastRequest
            {
                Messages = new List<BroadcastMessage>
                {
                    new BroadcastMessage
                    {
                        Topic = $"space:{spaceId}",
                        Event = eventName,
                        Payload = payload,
                    },
                },
            });

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Never surface the service-role key or response body to callers.
                throw new InvalidOperationException(
                    $"Supabase realtime broadcast of '{eventName}' failed with status {(int)response.StatusCode}.");
            }
        }

        // ----- Supabase broadcast wire models (transport-only) -----

        private sealed class BroadcastRequest
        {
            [JsonPropertyName("messages")]
            public List<BroadcastMessage> Messages { get; set; } = new();
        }

        private sealed class BroadcastMessage
        {
            [JsonPropertyName("topic")]
            public string Topic { get; set; } = string.Empty;

            [JsonPropertyName("event")]
            public string Event { get; set; } = string.Empty;

            [JsonPropertyName("payload")]
            public object Payload { get; set; } = new();

            [JsonPropertyName("private")]
            public bool Private { get; set; }
        }
    }
}
