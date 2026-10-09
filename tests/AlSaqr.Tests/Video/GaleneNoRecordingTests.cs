using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// C10: nothing the backend sends to Galène may enable recording. Checks the group
    /// definition it creates and the permissions of every token it signs (presenter,
    /// viewer, operator), as opposed to the source scan in EventVideoWiringTests.
    /// </summary>
    public class GaleneNoRecordingTests
    {
        private static GaleneConfig Config(string adminUrl) => new()
        {
            AdminUrl = adminUrl,
            PublicUrl = "https://video.test",
            AdminUsername = "alsaqr-admin",
            AdminPassword = "admin-password-value",
            TokenSecret = "test-galene-token-secret-0123456",
        };

        // Room creation and token minting never reach the operator, so a real (unused) one will do.
        private static GaleneVideoService Service(RecordingHandler admin)
        {
            var config = Options.Create(Config("https://galene.internal:8443"));
            return new GaleneVideoService(
                new HttpClient(admin), config, new GaleneOperator(config, NullLogger<GaleneOperator>.Instance));
        }

        private static string[] Permissions(string token) =>
            JwtJson.Payload(token).GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToArray();

        private static void AssertNoRecording(IEnumerable<string> permissions) =>
            Assert.DoesNotContain(permissions, p => p.Equals("record", StringComparison.OrdinalIgnoreCase)
                                                 || p.Equals("unrecord", StringComparison.OrdinalIgnoreCase));

        [Fact]
        public async Task CreatedGroupDefinition_DisallowsRecording()
        {
            var admin = new RecordingHandler();
            var service = Service(admin);

            await service.CreateRoomAsync("stream-a", 50, new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc));

            var group = admin.Requests.Single(r => r.Method == HttpMethod.Put && r.Url.EndsWith("/.groups/stream-a/"));
            using var body = JsonDocument.Parse(group.Body);
            Assert.Equal(JsonValueKind.False, body.RootElement.GetProperty("allow-recording").ValueKind);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PresenterAndViewerTokens_NeverCarryRecordPermissions(bool canPublish)
        {
            var service = Service(new RecordingHandler());

            var token = service.MintJoinToken("stream-a", "a0:01", canPublish).Token;

            AssertNoRecording(Permissions(token));
        }

        [Fact]
        public async Task OperatorToken_NeverCarriesRecordPermissions()
        {
            // A loopback stand-in for Galène's WebSocket: captures the operator's join
            // token, then refuses the join so ActAsync returns at once.
            var joinToken = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var galene = builder.Build();
            galene.UseWebSockets();
            galene.Map("/ws", async (HttpContext ctx) =>
            {
                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var result = await ws.ReceiveAsync(buffer, ctx.RequestAborted);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    using var message = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (message.RootElement.GetProperty("type").GetString() != "join")
                        continue;

                    joinToken.TrySetResult(message.RootElement.GetProperty("token").GetString()!);
                    await ws.SendAsync(
                        Encoding.UTF8.GetBytes("""{"type":"joined","kind":"fail"}"""),
                        WebSocketMessageType.Text, true, ctx.RequestAborted);
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    return;
                }
            });
            await galene.StartAsync();

            var op = new GaleneOperator(Options.Create(Config(galene.Urls.Single())), NullLogger<GaleneOperator>.Instance);
            var result = await op.ActAsync("stream-a", "kick", null);

            Assert.False(result.Admitted);
            var permissions = Permissions(await joinToken.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("op", permissions);
            AssertNoRecording(permissions);
        }
    }
}
