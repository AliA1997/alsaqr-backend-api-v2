using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// The go/no-go spike, kept as regression tests: every assumption the permission
    /// model makes about Galène, checked against a real server. Tests marked GATE
    /// must pass for Galène to stay the SFU.
    /// </summary>
    public class GaleneServerTests : IClassFixture<GaleneContainer>
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
        private readonly GaleneContainer _galene;
        private readonly GaleneVideoService _service;

        public GaleneServerTests(GaleneContainer galene)
        {
            _galene = galene;
            _service = Service(galene.Config);
        }

        private static GaleneVideoService Service(GaleneConfig config) =>
            new(new HttpClient(), Options.Create(config),
                new GaleneOperator(Options.Create(config), NullLogger<GaleneOperator>.Instance));

        private async Task<string> NewRoom(int maxParticipants = 50)
        {
            var room = $"stream-{Guid.NewGuid()}";
            await _service.CreateRoomAsync(room, maxParticipants, DateTime.UtcNow.AddHours(4));
            return room;
        }

        private async Task<(GaleneTestClient Client, string[] Permissions)> Join(string room, string identity, bool canPublish)
        {
            var client = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);
            var joined = await client.JoinAsync(room, _service.MintJoinToken(room, identity, canPublish).Token);
            Assert.Equal("join", GaleneTestClient.Kind(joined));
            return (client, GaleneTestClient.Permissions(joined));
        }

        [Fact]
        public async Task PresenterToken_JoinsWithPresentPermission()
        {
            var room = await NewRoom();

            var (client, permissions) = await Join(room, "a0:0a", canPublish: true);
            await using var _ = client;

            Assert.Contains("present", permissions);
        }

        [Fact]
        public async Task ViewerToken_JoinsWithoutPresentPermission()
        {
            var room = await NewRoom();

            var (client, permissions) = await Join(room, "a0:0b", canPublish: false);
            await using var _ = client;

            Assert.DoesNotContain("present", permissions);
        }

        [Fact]
        public async Task ForgedToken_IsRefused()
        {
            var room = await NewRoom();
            var forger = new GaleneConfig
            {
                PublicUrl = _galene.BaseUrl,
                TokenSecret = "some-other-secret-0123456789-abc",
            };
            var forged = GaleneTokens.Write(forger, room, "a0:0c", new[] { "present" }, TimeSpan.FromSeconds(90), out _);

            await using var client = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);
            var joined = await client.JoinAsync(room, forged);

            Assert.Equal("fail", GaleneTestClient.Kind(joined));
        }

        [Fact]
        public async Task TokenForAnotherRoom_IsRefused()
        {
            var roomA = await NewRoom();
            var roomB = await NewRoom();

            await using var client = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);
            var joined = await client.JoinAsync(roomA, _service.MintJoinToken(roomB, "a0:01", true).Token);

            Assert.Equal("fail", GaleneTestClient.Kind(joined));
        }

        [Fact] // GATE: server-side demote
        public async Task Unpresent_RevokesPublishOnLiveConnection()
        {
            var room = await NewRoom();
            var (client, _) = await Join(room, "a0:0a", canPublish: true);
            await using var _c = client;

            await _service.SetPublishAsync(room, "a0:0a", canPublish: false);

            var change = await client.WaitForAsync(
                m => GaleneTestClient.Type(m) == "joined" && GaleneTestClient.Kind(m) == "change", Wait);
            Assert.DoesNotContain("present", GaleneTestClient.Permissions(change));
        }

        [Fact]
        public async Task Present_GrantsPublishOnLiveConnection()
        {
            var room = await NewRoom();
            var (client, _) = await Join(room, "a0:0b", canPublish: false);
            await using var _c = client;

            await _service.SetPublishAsync(room, "a0:0b", canPublish: true);

            var change = await client.WaitForAsync(
                m => GaleneTestClient.Type(m) == "joined" && GaleneTestClient.Kind(m) == "change", Wait);
            Assert.Contains("present", GaleneTestClient.Permissions(change));
        }

        [Fact] // GATE: server-side kick
        public async Task Kick_DisconnectsOnlyTheTarget()
        {
            var room = await NewRoom();
            var (target, _) = await Join(room, "a0:0d", canPublish: false);
            var (bystander, _) = await Join(room, "a0:0e", canPublish: false);
            await using var _t = target;
            await using var _b = bystander;

            await _service.RemoveParticipantsAsync(room, new[] { "a0:0d" });

            Assert.True(await target.WaitForLeaveAsync(Wait));
            Assert.False(await bystander.WaitForLeaveAsync(TimeSpan.FromSeconds(1)));
        }

        [Fact] // GATE: server-side end
        public async Task DeleteRoom_DisconnectsEveryone_AndRefusesNewJoins()
        {
            var room = await NewRoom();
            var token = _service.MintJoinToken(room, "a0:0f", false).Token;
            var (client, _) = await Join(room, "a0:01", canPublish: true);
            await using var _c = client;

            await _service.DeleteRoomAsync(room);

            Assert.True(await client.WaitForLeaveAsync(Wait));
            await using var late = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);
            Assert.Equal("fail", GaleneTestClient.Kind(await late.JoinAsync(room, token)));
        }

        [Fact]
        public async Task MaxClients_IsEnforced()
        {
            var room = await NewRoom(maxParticipants: 2);
            var (first, _) = await Join(room, "a0:11", false);
            var (second, _) = await Join(room, "a0:12", false);
            await using var _1 = first;
            await using var _2 = second;

            await using var third = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);
            var joined = await third.JoinAsync(room, _service.MintJoinToken(room, "a0:13", false).Token);

            Assert.Equal("fail", GaleneTestClient.Kind(joined));
        }

        [Fact]
        public async Task DuplicateUsername_KeepsBothConnections()
        {
            // Documents correction 15. If Galène ever starts replacing the older
            // connection itself, this fails: then remove the HadLiveConnection kick.
            var room = await NewRoom();
            var (older, _) = await Join(room, "a0:10", false);
            var (newer, _) = await Join(room, "a0:10", false);
            await using var _o = older;
            await using var _n = newer;

            Assert.False(await older.WaitForLeaveAsync(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task TokenAudienceHost_DifferentFromConnectionHost_IsAccepted()
        {
            // Production clients connect via the public host while the operator uses
            // the internal AdminUrl, with the same audience. If this fails, set
            // Galène's "canonicalHost" to the public host and connect the operator
            // through the public URL instead (Task 10, After merge).
            var room = await NewRoom();
            var publicView = new GaleneConfig { PublicUrl = "https://video.test", TokenSecret = GaleneContainer.TokenSecret };
            var token = GaleneTokens.Write(publicView, room, "a0:01", Array.Empty<string>(), TimeSpan.FromSeconds(90), out _);

            await using var client = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);

            Assert.Equal("join", GaleneTestClient.Kind(await client.JoinAsync(room, token)));
        }
    }
}
