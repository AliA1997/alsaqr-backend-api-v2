using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// <see cref="IVideoSfuService"/> on Galène: one group per stream, created and
    /// deleted over the admin HTTP API (HTTP Basic, an "admin" user in Galène's
    /// config.json); room tokens signed with GaleneTokens; live moderation through
    /// IGaleneOperator. This is the ONLY place the admin password and token secret
    /// are used.
    /// </summary>
    public sealed class GaleneVideoService : IVideoSfuService
    {
        // C8: the token authorizes ENTRY; the session outlives it.
        private static readonly TimeSpan TokenTtl = TimeSpan.FromSeconds(90);

        private static readonly IReadOnlyCollection<string> PresenterPermissions = new[] { "present" };

        // Receive-only (C4). Task 3's gate tests decide whether Galène needs
        // ["observe"] here instead of no permissions at all.
        private static readonly IReadOnlyCollection<string> ViewerPermissions = Array.Empty<string>();

        private readonly HttpClient _http;
        private readonly GaleneConfig _config;
        private readonly IGaleneOperator _operator;

        public GaleneVideoService(HttpClient http, IOptions<GaleneConfig> config, IGaleneOperator galeneOperator)
        {
            _http = http;
            _config = config.Value;
            _operator = galeneOperator;
        }

        public async Task CreateRoomAsync(
            string roomName, int maxParticipants, DateTime notAfterUtc, CancellationToken ct = default)
        {
            await SendAsync(
                HttpMethod.Put,
                GroupPath(roomName),
                JsonContent.Create(new GroupDefinition
                {
                    MaxClients = maxParticipants,
                    AllowRecording = false,   // C10
                    Expires = notAfterUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                }),
                createOnly: true,
                tolerateNotFound: false,
                ct);

            try
            {
                await SendAsync(
                    HttpMethod.Put,
                    $"{GroupPath(roomName)}.keys",
                    JsonContent.Create(
                        new KeySet
                        {
                            Keys =
                            {
                                new RoomKey { KeyId = GaleneTokens.KeyId, Value = GaleneTokens.KeyValue(_config) },
                            },
                        },
                        new MediaTypeHeaderValue("application/jwk-set+json")),
                    createOnly: false,
                    tolerateNotFound: false,
                    ct);
            }
            catch
            {
                // A group nobody can authenticate to is useless; don't leave it behind.
                await SendAsync(HttpMethod.Delete, GroupPath(roomName), null, false, true, CancellationToken.None);
                throw;
            }
        }

        public SfuJoinToken MintJoinToken(string roomName, string identity, bool canPublish)
        {
            var token = GaleneTokens.Write(
                _config, roomName, identity, canPublish ? PresenterPermissions : ViewerPermissions, TokenTtl, out var expiresAt);

            return new SfuJoinToken(token, GaleneTokens.WebSocketUrl(_config.PublicUrl), roomName, expiresAt);
        }

        public async Task SetPublishAsync(string roomName, string identity, bool canPublish, CancellationToken ct = default)
        {
            // Unlike kicks, this must not be silent: a failed demote leaves someone publishing.
            if (!await _operator.ActAsync(roomName, canPublish ? "present" : "unpresent", new[] { identity }, ct))
                throw new InvalidOperationException($"Galène refused the publish change for room {roomName}.");
        }

        public async Task RemoveParticipantsAsync(
            string roomName, IReadOnlyCollection<string> identities, CancellationToken ct = default)
        {
            if (identities.Count == 0)
                return;

            await _operator.ActAsync(roomName, "kick", identities, ct);
        }

        public async Task DeleteRoomAsync(string roomName, CancellationToken ct = default)
        {
            await _operator.ActAsync(roomName, "kick", null, ct);
            await SendAsync(HttpMethod.Delete, GroupPath(roomName), null, false, tolerateNotFound: true, ct);
        }

        // ----- internals -----

        private static string GroupPath(string roomName) =>
            $"/galene-api/v0/.groups/{Uri.EscapeDataString(roomName)}/";

        private async Task SendAsync(
            HttpMethod method, string path, HttpContent? content, bool createOnly, bool tolerateNotFound, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, _config.AdminUrl.TrimEnd('/') + path) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.AdminUsername}:{_config.AdminPassword}")));

            if (createOnly)
                request.Headers.TryAddWithoutValidation("If-None-Match", "*");   // never overwrite a live room

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode || (tolerateNotFound && response.StatusCode == HttpStatusCode.NotFound))
                return;

            // Never echo the response body: it may carry server paths or configuration.
            throw new InvalidOperationException(
                $"Galène admin {method} {path} failed with status {(int)response.StatusCode}.");
        }

        // ----- Galène admin API wire models (transport-only) -----

        private sealed class GroupDefinition
        {
            [JsonPropertyName("max-clients")]
            public int MaxClients { get; set; }

            [JsonPropertyName("allow-recording")]
            public bool AllowRecording { get; set; }

            /// <summary>RFC 3339 access cutoff (C11 backstop).</summary>
            [JsonPropertyName("expires")]
            public string Expires { get; set; } = string.Empty;
        }

        private sealed class KeySet
        {
            [JsonPropertyName("keys")]
            public List<RoomKey> Keys { get; set; } = new();
        }

        private sealed class RoomKey
        {
            [JsonPropertyName("kty")]
            public string KeyType { get; set; } = "oct";

            [JsonPropertyName("alg")]
            public string Algorithm { get; set; } = "HS256";

            [JsonPropertyName("kid")]
            public string KeyId { get; set; } = string.Empty;

            [JsonPropertyName("k")]
            public string Value { get; set; } = string.Empty;
        }
    }
}
