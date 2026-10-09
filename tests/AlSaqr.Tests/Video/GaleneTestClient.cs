using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AlSaqr.Infrastructure.Video;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// A minimal Galène participant speaking the WebSocket protocol (no media), so
    /// tests can observe what a real client would be told.
    /// </summary>
    public sealed class GaleneTestClient : IAsyncDisposable
    {
        private readonly ClientWebSocket _ws = new();
        private readonly string _id = Guid.NewGuid().ToString("N");

        public bool IsOpen => _ws.State == WebSocketState.Open;

        public static async Task<GaleneTestClient> ConnectAsync(string baseUrl)
        {
            var client = new GaleneTestClient();
            await client._ws.ConnectAsync(new Uri(GaleneTokens.WebSocketUrl(baseUrl)), CancellationToken.None);
            await client.SendAsync(new { type = "handshake", version = new[] { "2" }, id = client._id });
            return client;
        }

        /// <summary>Joins with a token; returns the "joined" message (kind "join" or "fail").</summary>
        public async Task<JsonElement> JoinAsync(string group, string token)
        {
            await SendAsync(new { type = "join", kind = "join", group, token });
            return await WaitForAsync(m => Type(m) == "joined" && Kind(m) is "join" or "fail");
        }

        /// <summary>True when Galène tells us we left the group, or closes the socket.</summary>
        public async Task<bool> WaitForLeaveAsync(TimeSpan timeout)
        {
            try
            {
                await WaitForAsync(m => Type(m) == "joined" && Kind(m) == "leave", timeout);
                return true;
            }
            catch (WebSocketException)
            {
                return true;
            }
            catch (EndOfStreamException)
            {
                return true;
            }
            catch (TimeoutException)
            {
                // We stopped waiting (which aborts our own socket); no leave was observed.
                return false;
            }
        }

        public async Task<JsonElement> WaitForAsync(Func<JsonElement, bool> match, TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
            while (true)
            {
                JsonElement message;
                try
                {
                    message = await ReceiveAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Galène did not send the expected message in time.");
                }

                if (Type(message) == "ping")
                {
                    await SendAsync(new { type = "pong" });
                    continue;
                }

                if (match(message))
                    return message;
            }
        }

        public static string? Type(JsonElement m) => m.TryGetProperty("type", out var t) ? t.GetString() : null;

        public static string? Kind(JsonElement m) => m.TryGetProperty("kind", out var k) ? k.GetString() : null;

        public static string[] Permissions(JsonElement joined) =>
            joined.TryGetProperty("permissions", out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().Select(x => x.GetString()!).ToArray()
                : Array.Empty<string>();

        private Task SendAsync(object message) =>
            _ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, CancellationToken.None);

        private async Task<JsonElement> ReceiveAsync(CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            using var body = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new EndOfStreamException("Galène closed the connection.");
                body.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            using var doc = JsonDocument.Parse(body.ToArray());
            return doc.RootElement.Clone();
        }

        public async ValueTask DisposeAsync()
        {
            if (IsOpen)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // already gone
                }
            }

            _ws.Dispose();
        }
    }
}
