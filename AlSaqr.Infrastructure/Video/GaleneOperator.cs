using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// Mid-call moderation for Galène. Galène's admin API manages groups but not live
    /// connections, so the backend joins the group briefly as a hidden operator
    /// ("op" permission), applies a useraction (present / unpresent / kick) to the
    /// matching connections, and leaves. Clients hide the operator's username.
    /// </summary>
    public interface IGaleneOperator
    {
        /// <param name="roomName">The group to act in.</param>
        /// <param name="kind">"present", "unpresent" or "kick".</param>
        /// <param name="usernames">Targets by Galène username; null means everyone but the operator.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Admitted = false when Galène refused the operator's join (typically: the group no longer exists); Matched = connections the action was sent to.</returns>
        Task<GaleneActResult> ActAsync(string roomName, string kind, IReadOnlyCollection<string>? usernames, CancellationToken ct = default);
    }

    public sealed record GaleneActResult(bool Admitted, int Matched)
    {
        public static GaleneActResult Refused { get; } = new(false, 0);
    }

    public sealed class GaleneOperator : IGaleneOperator
    {
        public const string Username = GaleneTokens.OperatorUsername;

        private static readonly TimeSpan TokenTtl = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(3);

        // After joining, Galène announces the connected users. Stop listening once
        // nothing new has arrived for this long (or every target has been seen).
        private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(400);

        private static readonly string[] OperatorPermissions = { "op" };

        private readonly GaleneConfig _config;
        private readonly ILogger<GaleneOperator> _logger;

        public GaleneOperator(IOptions<GaleneConfig> config, ILogger<GaleneOperator> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task<GaleneActResult> ActAsync(
            string roomName, string kind, IReadOnlyCollection<string>? usernames, CancellationToken ct = default)
        {
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            session.CancelAfter(TimeSpan.FromSeconds(10));

            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(GaleneTokens.WebSocketUrl(_config.AdminUrl)), session.Token);

            var sendLock = new SemaphoreSlim(1, 1);
            var inbox = Channel.CreateUnbounded<JsonElement>();
            var pump = PumpAsync(ws, inbox.Writer, sendLock, session.Token);

            var ownId = Guid.NewGuid().ToString("N");
            await SendAsync(ws, sendLock, new { type = "handshake", version = new[] { "2" }, id = ownId }, session.Token);
            await SendAsync(ws, sendLock, new
            {
                type = "join",
                kind = "join",
                group = roomName,
                token = GaleneTokens.Write(_config, roomName, Username, OperatorPermissions, TokenTtl, out _),
            }, session.Token);

            var users = new Dictionary<string, string>();   // Galène client id → username
            var joined = false;
            var clock = Stopwatch.StartNew();
            var lastNews = clock.Elapsed;

            while (true)
            {
                if (joined && usernames != null && usernames.All(users.ContainsValue))
                    break;

                var wait = joined ? QuietPeriod - (clock.Elapsed - lastNews) : JoinTimeout - clock.Elapsed;
                if (wait <= TimeSpan.Zero)
                    break;

                var message = await NextAsync(inbox.Reader, wait, session.Token);
                if (message is not JsonElement m)
                    break;

                switch (Str(m, "type"))
                {
                    case "joined" when Str(m, "kind") == "join":
                        joined = true;
                        lastNews = clock.Elapsed;
                        break;
                    case "joined" when Str(m, "kind") == "fail":
                        _logger.LogWarning("Galène refused the operator for room {Room}.", roomName);
                        return GaleneActResult.Refused;
                    case "user":
                        var id = Str(m, "id");
                        if (id != null && id != ownId)
                        {
                            if (Str(m, "kind") == "delete")
                                users.Remove(id);
                            else
                                users[id] = Str(m, "username") ?? string.Empty;
                            lastNews = clock.Elapsed;
                        }
                        break;
                }
            }

            if (!joined)
            {
                _logger.LogWarning("Galène did not admit the operator to room {Room} in time.", roomName);
                return GaleneActResult.Refused;
            }

            var targets = users
                .Where(u => usernames == null ? u.Value != Username : usernames.Contains(u.Value))
                .Select(u => u.Key)
                .ToList();

            foreach (var dest in targets)
            {
                await SendAsync(ws, sendLock, new
                {
                    type = "useraction",
                    kind,
                    source = ownId,
                    dest,
                    username = Username,
                }, session.Token);
            }

            await SendAsync(ws, sendLock, new { type = "join", kind = "leave", group = roomName }, session.Token);
            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, session.Token);
            await AwaitQuietly(pump);

            _logger.LogInformation(
                "Galène operator applied {Kind} to {Count} connection(s) in room {Room}.", kind, targets.Count, roomName);
            return new GaleneActResult(true, targets.Count);
        }

        private static async Task<JsonElement?> NextAsync(ChannelReader<JsonElement> reader, TimeSpan wait, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(wait);
            try
            {
                return await reader.ReadAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;   // waited long enough
            }
            catch (ChannelClosedException)
            {
                return null;   // socket closed
            }
        }

        // Reads every message into the inbox and answers pings. Cancelling a
        // ClientWebSocket receive aborts the socket, so timeouts live on the channel.
        private static async Task PumpAsync(
            ClientWebSocket ws, ChannelWriter<JsonElement> inbox, SemaphoreSlim sendLock, CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    using var body = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(buffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        body.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    using var doc = JsonDocument.Parse(body.ToArray());
                    var message = doc.RootElement.Clone();

                    if (Str(message, "type") == "ping")
                        await SendAsync(ws, sendLock, new { type = "pong" }, ct);
                    else
                        await inbox.WriteAsync(message, ct);
                }
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or JsonException)
            {
                // The session is over either way; ActAsync decides what that means.
            }
            finally
            {
                inbox.TryComplete();
            }
        }

        private static async Task SendAsync(ClientWebSocket ws, SemaphoreSlim sendLock, object message, CancellationToken ct)
        {
            await sendLock.WaitAsync(ct);
            try
            {
                await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, ct);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private static async Task AwaitQuietly(Task task)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // Galène will drop the closed socket on its own.
            }
        }

        private static string? Str(JsonElement m, string name) =>
            m.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
