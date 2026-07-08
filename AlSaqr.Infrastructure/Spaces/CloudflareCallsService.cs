using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Spaces
{
    /// <summary>
    /// Result of publishing a track: the client OFFERED, the SFU ANSWERED.
    /// </summary>
    public sealed record SfuPublishResult(string SessionId, string TrackName, string AnswerSdp);

    /// <summary>
    /// Result of pulling a track: the SFU OFFERS, the client answers via renegotiate.
    /// </summary>
    public sealed record SfuSubscribeResult(string SessionId, string OfferSdp);

    /// <summary>
    /// Proxy for the Cloudflare Calls (Realtime SFU) HTTPS API (specs/audio-spaces.md).
    /// This is the ONLY place the app secret is used; the client never talks to
    /// Cloudflare directly. Ephemerality is guaranteed by omission: this service
    /// exposes no egress, recording, or transcription call — such code paths must
    /// not be added.
    /// </summary>
    public interface ICloudflareCallsService
    {
        /// <summary>
        /// Creates a session and publishes the caller's audio track from their SDP
        /// offer. Returns the SFU's answer plus the session/track identifiers other
        /// participants use to pull the track.
        /// </summary>
        Task<SfuPublishResult> PublishAsync(string sdpOffer, string mid, CancellationToken ct = default);

        /// <summary>
        /// Pulls <paramref name="trackName"/> from <paramref name="pubSessionId"/>
        /// into the caller's existing session (or a new one when
        /// <paramref name="existingSessionId"/> is null). Each call is scoped to a
        /// single track so adding the nth speaker never disturbs other subscriptions.
        /// </summary>
        Task<SfuSubscribeResult> SubscribeAsync(
            string? existingSessionId, string pubSessionId, string trackName, CancellationToken ct = default);

        /// <summary>
        /// Forwards the client's SDP answer for a pending renegotiation. Ordering is
        /// owned by the client; this just forwards in the order received.
        /// </summary>
        Task RenegotiateAsync(string sessionId, string sdpAnswer, CancellationToken ct = default);

        /// <summary>
        /// Force-closes a published track so the SFU stops serving audio for it.
        /// Used on leave, demotion, end, and reap. Best-effort: a session that is
        /// already gone is not an error.
        /// </summary>
        Task CloseTrackAsync(string sessionId, string mid, CancellationToken ct = default);
    }

    public sealed class CloudflareCallsService : ICloudflareCallsService
    {
        private const string Local = "local";
        private const string Remote = "remote";

        private readonly HttpClient _httpClient;
        private readonly CloudflareCallsConfig _config;

        public CloudflareCallsService(HttpClient httpClient, IOptions<CloudflareCallsConfig> config)
        {
            _httpClient = httpClient;
            _config = config.Value;
        }

        public async Task<SfuPublishResult> PublishAsync(string sdpOffer, string mid, CancellationToken ct = default)
        {
            var sessionId = await CreateSessionAsync(ct);
            var trackName = Guid.NewGuid().ToString("N");

            var response = await PostTracksAsync(
                sessionId,
                new TracksRequest
                {
                    SessionDescription = new SessionDescription { Type = "offer", Sdp = sdpOffer },
                    Tracks = new List<TrackRequest>
                    {
                        new TrackRequest { Location = Local, Mid = mid, TrackName = trackName },
                    },
                },
                ct);

            if (response.SessionDescription?.Sdp is not { Length: > 0 } answerSdp)
                throw new InvalidOperationException("Cloudflare Calls returned no SDP answer for the publish offer.");

            return new SfuPublishResult(sessionId, trackName, answerSdp);
        }

        public async Task<SfuSubscribeResult> SubscribeAsync(
            string? existingSessionId, string pubSessionId, string trackName, CancellationToken ct = default)
        {
            var sessionId = existingSessionId ?? await CreateSessionAsync(ct);

            var response = await PostTracksAsync(
                sessionId,
                new TracksRequest
                {
                    Tracks = new List<TrackRequest>
                    {
                        new TrackRequest { Location = Remote, SessionId = pubSessionId, TrackName = trackName },
                    },
                },
                ct);

            if (response.SessionDescription?.Sdp is not { Length: > 0 } offerSdp)
                throw new InvalidOperationException("Cloudflare Calls returned no SDP offer for the track pull.");

            return new SfuSubscribeResult(sessionId, offerSdp);
        }

        public async Task RenegotiateAsync(string sessionId, string sdpAnswer, CancellationToken ct = default)
        {
            using var request = NewRequest(HttpMethod.Put, $"sessions/{sessionId}/renegotiate");
            request.Content = JsonContent.Create(new RenegotiateRequest
            {
                SessionDescription = new SessionDescription { Type = "answer", Sdp = sdpAnswer },
            });

            var response = await _httpClient.SendAsync(request, ct);
            await ThrowIfFailed(response, "renegotiate", ct);
        }

        public async Task CloseTrackAsync(string sessionId, string mid, CancellationToken ct = default)
        {
            using var request = NewRequest(HttpMethod.Put, $"sessions/{sessionId}/tracks/close");
            request.Content = JsonContent.Create(new CloseTracksRequest
            {
                Tracks = new List<CloseTrack> { new CloseTrack { Mid = mid } },
                Force = true,
            });

            var response = await _httpClient.SendAsync(request, ct);

            // Best-effort teardown: the session may already be gone (peer died and
            // the SFU reaped it). Anything else is a real failure.
            if (!response.IsSuccessStatusCode
                && response.StatusCode != System.Net.HttpStatusCode.NotFound
                && response.StatusCode != System.Net.HttpStatusCode.Gone)
            {
                throw new InvalidOperationException(
                    $"Cloudflare Calls tracks/close failed with status {(int)response.StatusCode}.");
            }
        }

        private async Task<string> CreateSessionAsync(CancellationToken ct)
        {
            using var request = NewRequest(HttpMethod.Post, "sessions/new");

            var response = await _httpClient.SendAsync(request, ct);
            await ThrowIfFailed(response, "sessions/new", ct);

            var parsed = await response.Content.ReadFromJsonAsync<NewSessionResponse>(cancellationToken: ct);
            if (parsed?.SessionId is not { Length: > 0 } sessionId)
                throw new InvalidOperationException("Cloudflare Calls returned no session id.");

            return sessionId;
        }

        private async Task<TracksResponse> PostTracksAsync(string sessionId, TracksRequest body, CancellationToken ct)
        {
            using var request = NewRequest(HttpMethod.Post, $"sessions/{sessionId}/tracks/new");
            request.Content = JsonContent.Create(body);

            var response = await _httpClient.SendAsync(request, ct);
            await ThrowIfFailed(response, "tracks/new", ct);

            var parsed = await response.Content.ReadFromJsonAsync<TracksResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Cloudflare Calls returned an empty tracks response.");

            var trackError = parsed.Tracks?.FirstOrDefault(t => !string.IsNullOrEmpty(t.ErrorDescription));
            if (trackError != null)
                throw new InvalidOperationException($"Cloudflare Calls track error: {trackError.ErrorDescription}");

            return parsed;
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string path)
        {
            var url = $"{_config.BaseUrl.TrimEnd('/')}/apps/{_config.AppId}/{path}";
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("Authorization", $"Bearer {_config.AppSecret}");
            return request;
        }

        private static async Task ThrowIfFailed(HttpResponseMessage response, string operation, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
                return;

            // Never echo the response body verbatim into an exception a client might
            // see — status + operation is enough to diagnose without leaking config.
            await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Cloudflare Calls {operation} failed with status {(int)response.StatusCode}.");
        }

        // ----- Cloudflare Calls wire models (transport-only; not domain DTOs) -----

        private sealed class NewSessionResponse
        {
            [JsonPropertyName("sessionId")]
            public string? SessionId { get; set; }
        }

        private sealed class SessionDescription
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("sdp")]
            public string Sdp { get; set; } = string.Empty;
        }

        private sealed class TrackRequest
        {
            [JsonPropertyName("location")]
            public string Location { get; set; } = string.Empty;

            [JsonPropertyName("mid")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Mid { get; set; }

            [JsonPropertyName("trackName")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? TrackName { get; set; }

            [JsonPropertyName("sessionId")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? SessionId { get; set; }
        }

        private sealed class TracksRequest
        {
            [JsonPropertyName("sessionDescription")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public SessionDescription? SessionDescription { get; set; }

            [JsonPropertyName("tracks")]
            public List<TrackRequest> Tracks { get; set; } = new();
        }

        private sealed class TracksResponse
        {
            [JsonPropertyName("requiresImmediateRenegotiation")]
            public bool RequiresImmediateRenegotiation { get; set; }

            [JsonPropertyName("sessionDescription")]
            public SessionDescription? SessionDescription { get; set; }

            [JsonPropertyName("tracks")]
            public List<TrackResult>? Tracks { get; set; }
        }

        private sealed class TrackResult
        {
            [JsonPropertyName("mid")]
            public string? Mid { get; set; }

            [JsonPropertyName("trackName")]
            public string? TrackName { get; set; }

            [JsonPropertyName("sessionId")]
            public string? SessionId { get; set; }

            [JsonPropertyName("errorDescription")]
            public string? ErrorDescription { get; set; }
        }

        private sealed class RenegotiateRequest
        {
            [JsonPropertyName("sessionDescription")]
            public SessionDescription SessionDescription { get; set; } = new();
        }

        private sealed class CloseTracksRequest
        {
            [JsonPropertyName("tracks")]
            public List<CloseTrack> Tracks { get; set; } = new();

            [JsonPropertyName("force")]
            public bool Force { get; set; }
        }

        private sealed class CloseTrack
        {
            [JsonPropertyName("mid")]
            public string Mid { get; set; } = string.Empty;
        }
    }
}
