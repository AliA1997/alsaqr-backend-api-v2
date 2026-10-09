using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Tests.Video
{
    public class GaleneVideoServiceTests
    {
        private const string Secret = "test-galene-token-secret-0123456";
        private const string AdminPassword = "admin-password-value";

        private readonly RecordingHandler _handler = new();
        private readonly FakeGaleneOperator _operator = new();
        private readonly GaleneVideoService _service;

        public GaleneVideoServiceTests()
        {
            _service = new GaleneVideoService(
                new HttpClient(_handler),
                Options.Create(new GaleneConfig
                {
                    AdminUrl = "http://galene.internal:8443/",
                    PublicUrl = "https://video.test",
                    AdminUsername = "alsaqr-admin",
                    AdminPassword = AdminPassword,
                    TokenSecret = Secret,
                }),
                _operator);
        }

        private static string[] Permissions(string token) =>
            JwtJson.Payload(token).GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToArray();

        [Fact]
        public void PresenterToken_HasPresentPermission_AndRoomAudience()
        {
            var token = _service.MintJoinToken("stream-a", "a0:01", canPublish: true).Token;

            Assert.Equal(new[] { "present" }, Permissions(token));
            Assert.Equal("https://video.test/group/stream-a/", JwtJson.Payload(token).GetProperty("aud").GetString());
            Assert.Equal("a0:01", JwtJson.Payload(token).GetProperty("sub").GetString());
        }

        [Fact]
        public void ViewerToken_CannotPresent()
        {
            var token = _service.MintJoinToken("stream-a", "a0:01", canPublish: false).Token;

            Assert.DoesNotContain("present", Permissions(token));
            Assert.DoesNotContain("op", Permissions(token));
            Assert.DoesNotContain("record", Permissions(token));
        }

        [Fact]
        public void Token_IsScopedToOneRoom_AndLivesAtMost90Seconds()
        {
            var token = _service.MintJoinToken("stream-a", "a0:01", canPublish: false).Token;

            var payload = JwtJson.Payload(token);
            Assert.InRange(payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64(), 1, 90);
            Assert.Equal("HS256", JwtJson.Header(token).GetProperty("alg").GetString());
            Assert.Equal(GaleneTokens.KeyId, JwtJson.Header(token).GetProperty("kid").GetString());

            // The signature verifies with the room secret (what Galène checks).
            new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
            }, out _);
        }

        [Fact]
        public void MintJoinToken_ReturnsPublicWebSocketUrl_AndGroup()
        {
            var token = _service.MintJoinToken("stream-a", "a0:01", canPublish: false);

            Assert.Equal("wss://video.test/ws", token.WsUrl);
            Assert.Equal("stream-a", token.Group);
            Assert.DoesNotContain(Secret, token.Token);
        }

        [Fact]
        public async Task CreateRoom_PutsGroupWithCostGuards_ThenInstallsKey()
        {
            var notAfter = new DateTime(2026, 10, 4, 18, 5, 0, DateTimeKind.Utc);

            await _service.CreateRoomAsync("stream-a", maxParticipants: 50, notAfter);

            Assert.Equal(2, _handler.Requests.Count);
            var group = _handler.Requests[0];
            Assert.Equal(HttpMethod.Put, group.Method);
            Assert.Equal("http://galene.internal:8443/galene-api/v0/.groups/stream-a/", group.Url);
            Assert.Equal("*", group.Headers["If-None-Match"]);
            Assert.StartsWith("Basic ", group.Headers["Authorization"]);
            Assert.DoesNotContain(AdminPassword, group.Headers["Authorization"]);
            using (var body = JsonDocument.Parse(group.Body))
            {
                Assert.Equal(50, body.RootElement.GetProperty("max-clients").GetInt32());
                Assert.False(body.RootElement.GetProperty("allow-recording").GetBoolean());
                Assert.Equal("2026-10-04T18:05:00Z", body.RootElement.GetProperty("expires").GetString());
            }

            var keys = _handler.Requests[1];
            Assert.Equal("http://galene.internal:8443/galene-api/v0/.groups/stream-a/.keys", keys.Url);
            using var jwks = JsonDocument.Parse(keys.Body);
            var key = jwks.RootElement.GetProperty("keys")[0];
            Assert.Equal("oct", key.GetProperty("kty").GetString());
            Assert.Equal("HS256", key.GetProperty("alg").GetString());
            Assert.Equal(GaleneTokens.KeyId, key.GetProperty("kid").GetString());
            Assert.Equal(Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(Secret)), key.GetProperty("k").GetString());
        }

        [Fact]
        public async Task CreateRoom_WhenKeyInstallFails_DeletesTheGroup_AndThrows()
        {
            _handler.Respond = r => r.Url.EndsWith("/.keys") ? HttpStatusCode.BadRequest : HttpStatusCode.OK;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateRoomAsync("stream-a", 50, DateTime.UtcNow.AddHours(4)));

            Assert.Equal(HttpMethod.Delete, _handler.Requests.Last().Method);
            Assert.EndsWith("/.groups/stream-a/", _handler.Requests.Last().Url);
        }

        [Fact]
        public async Task SetPublish_UsesOperatorPresentAndUnpresent()
        {
            await _service.SetPublishAsync("stream-a", "a0:01", canPublish: true);
            await _service.SetPublishAsync("stream-a", "a0:01", canPublish: false);

            Assert.Equal(new[] { "present stream-a a0:01", "unpresent stream-a a0:01" }, _operator.Calls);
        }

        [Fact]
        public async Task SetPublish_WhenOperatorRefused_Throws()
        {
            // A silent no-op here would leave a demoted user publishing.
            _operator.Accept = false;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SetPublishAsync("stream-a", "a0:01", canPublish: false));
        }

        [Fact]
        public async Task SetPublish_Demote_WhenNoConnectionMatched_ThrowsNotConnected()
        {
            _operator.Matched = 0;

            await Assert.ThrowsAsync<ParticipantNotConnectedException>(
                () => _service.SetPublishAsync("stream-a", "a0:01", canPublish: false));
        }

        [Fact]
        public async Task SetPublish_Demote_WhenMatched_Succeeds()
        {
            _operator.Matched = 1;

            await _service.SetPublishAsync("stream-a", "a0:01", canPublish: false);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("Stream-A")]
        [InlineData("a/b")]
        [InlineData("-a")]
        [InlineData("alsaqr-operator")]
        public async Task InvalidRoomName_IsRejected_BeforeAnyRequest(string room)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateRoomAsync(room, 50, DateTime.UtcNow.AddHours(1)));
            await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteRoomAsync(room));
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetPublishAsync(room, "a0:01", true));
            Assert.Throws<ArgumentException>(() => _service.MintJoinToken(room, "a0:01", false));
            Assert.Empty(_handler.Requests);
            Assert.Empty(_operator.Calls);
        }

        [Theory]
        [InlineData("alsaqr-operator")]
        [InlineData("s:u")]
        [InlineData("a0:01\n")]
        [InlineData("")]
        public void InvalidIdentity_IsRejected(string identity)
        {
            Assert.Throws<ArgumentException>(() => _service.MintJoinToken("stream-a", identity, false));
        }

        [Fact]
        public async Task RealGeneratedNames_AreAccepted()
        {
            var stream = Guid.NewGuid();
            var room = $"stream-{stream}";
            var identity = $"{stream}:{Guid.NewGuid()}";

            await _service.CreateRoomAsync(room, 50, DateTime.UtcNow.AddHours(1));
            _service.MintJoinToken(room, identity, true);
            await _service.RemoveParticipantsAsync(room, new[] { identity });
        }

        [Fact]
        public async Task CreateRoom_WhenCleanupAlsoFails_StillThrowsTheKeyInstallError()
        {
            _handler.Respond = r => r.Url.EndsWith("/.keys") ? HttpStatusCode.BadRequest
                : r.Method == HttpMethod.Delete ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateRoomAsync("stream-a", 50, DateTime.UtcNow.AddHours(1)));

            Assert.Contains("400", ex.Message);
            Assert.Equal(HttpMethod.Delete, _handler.Requests.Last().Method);
        }

        [Fact]
        public async Task DeleteRoom_WhenOperatorUnreachable_StillDeletesTheGroup()
        {
            _operator.Throw = new System.Net.WebSockets.WebSocketException("unreachable");

            await _service.DeleteRoomAsync("stream-a");

            Assert.Equal(HttpMethod.Delete, Assert.Single(_handler.Requests).Method);
        }

        [Fact]
        public async Task RemoveParticipants_KicksOnlyGivenIdentities_AndSkipsEmpty()
        {
            await _service.RemoveParticipantsAsync("stream-a", Array.Empty<string>());
            await _service.RemoveParticipantsAsync("stream-a", new[] { "a0:11", "a0:12" });

            Assert.Equal(new[] { "kick stream-a a0:11,a0:12" }, _operator.Calls);
        }

        [Fact]
        public async Task DeleteRoom_KicksEveryone_ThenDeletes_ToleratingNotFound()
        {
            _handler.Status = HttpStatusCode.NotFound;
            _operator.Accept = false;   // group already gone

            await _service.DeleteRoomAsync("stream-a");   // must not throw

            Assert.Equal(new[] { "kick stream-a *" }, _operator.Calls);
            Assert.Equal(HttpMethod.Delete, Assert.Single(_handler.Requests).Method);
        }

        [Fact]
        public async Task ServerError_Throws_WithoutEchoingBodyOrCredentials()
        {
            _handler.Status = HttpStatusCode.InternalServerError;
            _handler.ResponseBody = "internal detail: /srv/galene/groups";

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateRoomAsync("stream-a", 50, DateTime.UtcNow.AddHours(4)));

            Assert.Contains("500", ex.Message);
            Assert.DoesNotContain("/srv/galene", ex.Message);
            Assert.DoesNotContain(AdminPassword, ex.Message);
            Assert.DoesNotContain(Secret, ex.Message);
        }

        private sealed class FakeGaleneOperator : IGaleneOperator
        {
            public List<string> Calls { get; } = new();

            public bool Accept { get; set; } = true;

            public int Matched { get; set; } = 1;

            public Exception? Throw { get; set; }

            public Task<GaleneActResult> ActAsync(
                string roomName, string kind, IReadOnlyCollection<string>? usernames, CancellationToken ct = default)
            {
                Calls.Add($"{kind} {roomName} {(usernames == null ? "*" : string.Join(",", usernames))}");
                if (Throw != null)
                    throw Throw;
                return Task.FromResult(Accept ? new GaleneActResult(true, Matched) : GaleneActResult.Refused);
            }
        }
    }
}
