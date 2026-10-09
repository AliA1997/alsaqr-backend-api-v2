using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// Channel-agnostic POST to Supabase Realtime's broadcast endpoint using the
    /// service-role key (specs/video-streaming-online-events.md §5).
    /// Known DRY deviation: SpaceEventBroadcaster contains the same POST, but it is
    /// frozen; it SHOULD move onto this client when the freeze lifts.
    /// </summary>
    public interface ISupabaseBroadcastClient
    {
        Task BroadcastAsync(string topic, string eventName, object payload, CancellationToken ct = default);
    }

    public sealed class SupabaseBroadcastClient : ISupabaseBroadcastClient
    {
        private readonly HttpClient _httpClient;
        private readonly SupabaseRealtimeConfig _config;

        public SupabaseBroadcastClient(HttpClient httpClient, IOptions<SupabaseRealtimeConfig> config)
        {
            _httpClient = httpClient;
            _config = config.Value;
        }

        public async Task BroadcastAsync(string topic, string eventName, object payload, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_config.Url) || string.IsNullOrWhiteSpace(_config.ServiceRoleSecret))
            {
                throw new InvalidOperationException("Supabase realtime Url and ServiceRoleSecret must be configured.");
            }

            var url = $"{_config.Url.TrimEnd('/')}/realtime/v1/api/broadcast";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("apikey", _config.ServiceRoleSecret);
            request.Headers.Add("Authorization", $"Bearer {_config.ServiceRoleSecret}");
            request.Content = JsonContent.Create(new BroadcastRequest
            {
                Messages = new List<BroadcastMessage>
                {
                    new BroadcastMessage { Topic = topic, Event = eventName, Payload = payload, Private = true },
                },
            });

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Never surface the service-role key or the response body.
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
            public bool Private { get; set; } = true;
        }
    }
}
