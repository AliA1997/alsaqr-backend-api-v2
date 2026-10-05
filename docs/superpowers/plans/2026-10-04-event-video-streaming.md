# Event Video Streaming (Plan B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Live video calls for online Meetup events. The organizer or group founder starts the call; attendees join and leave; the host promotes and demotes presenters; the host ends the call; a reaper disconnects dead clients and ends empty or 4-hour-old calls; participation metadata is purged after 90 days.

**Architecture:** The backend is a control plane only. Media runs through a self-hosted **Galène** SFU (MIT, Go, single binary with a built-in TURN server). The backend creates and deletes one Galène group per stream over Galène's admin HTTP API, signs short-lived HS256 room tokens whose `permissions` decide who may publish, and performs mid-call moderation (present/unpresent/kick) by joining the group briefly as a hidden operator over Galène's WebSocket protocol. Postgres (`video_streams`, `video_participants`) is the source of truth for who may be in which call. Supabase Realtime broadcasts lifecycle events on `event_video:{eventId}`. All authorization lives in `VideoStreamRepository`; the controller orchestrates repository → SFU → broadcast. The SFU sits behind `IVideoSfuService`, so the Janus fallback (see Task 3's gate) is a one-class swap.

**Tech Stack:** .NET 8, Supabase PostgREST client, Galène 1.2.1 (built from source, Docker), System.IdentityModel.Tokens.Jwt 7.5.1, `System.Net.WebSockets.ClientWebSocket`, xUnit + the Plan A Testcontainers harness.

**Spec:** `specs/video-streaming-online-events.md` (v2, with its 2026-10-04 SFU addendum). Product context: `specs/video-streaming-online-events.prd.md`. **Plan A (`docs/superpowers/plans/2026-10-04-event-video-prerequisites.md`) must be merged first.**

## Why Galène (decided 2026-10-04)

LiveKit is itself open source; Galène was chosen because it is cheaper to run and removes infrastructure:

- **CPU:** ~¼ core for a 100-person lecture; ~20 participants per core in many-to-many meetings ([galene.org](https://galene.org/)).
- **Built-in TURN server:** every other candidate needs a separate coturn deployment, which 10–25% of clients require ([Fora Soft comparison](https://www.forasoft.com/learn/video-streaming/articles-streaming/sfu-comparison-mediasoup-janus-livekit-jitsi-pion)).
- **Backend-signed HS256 tokens with a `present` permission**, which maps 1:1 onto the spec's "viewers receive, presenters publish" model.
- **More features:** simulcast and SVC (VP8/VP9), screen and multi-window sharing, admin HTTP API; MIT licence.
- **Known risk:** server-side kick and unpresent are only available through the operator WebSocket protocol, not the admin API, and it is a small project (one main maintainer). **Task 3 is a go/no-go spike**: if its gate tests fail and cannot be fixed within a day, stop and re-plan on Janus (GPLv3, full HTTP control API).

## Global Constraints

- Everything from Plan A's Global Constraints (net8.0, System.Text.Json only, constructor DI against interfaces, Docker required, no `-warnaserror`, no shared dev DB in tests).
- **Audio spaces are frozen:** no edits to `SpacesController.cs`, `Space`, `SpaceParticipant`, `SpaceRepository`, `CloudflareCallsService`, `SpaceEventBroadcaster`, `SpaceReaperService`. Every artifact here is a new file, except `Program.cs`, `AppSecrets.cs`, `appsettings.Development.json`, and the test fixture.
- Cost rules from the spec are binding. Exact values: token TTL **90 s**; `MaxParticipants` default **50** (Galène `max-clients`); `MaxPresenters` default **9** (host included); participant timeout **60 s**; reaper tick **30 s**; max stream duration **4 h**; rejoin grace **5 min**; retention **90 days**.
- **No recording, ever (C10):** every Galène group is created with `"allow-recording": false`, and no token ever carries the `record` permission. Task 10 has a source-scan test for it.
- Galène admin credentials and the room-token secret never appear in a response, log line, or exception message. Only `wsUrl`, `group`, and a minted room token leave the API.
- Video endpoints are not cached: they serve real-time state (spec §Rules, "Constitution compliance").
- Controllers never throw custom exceptions and never try/catch to map errors to HTTP. Repositories throw; `ExceptionHandlingMiddleware` maps.
- Never compare a `DateTime` read back from PostgREST against `DateTime.UtcNow` in C#. Time-window checks (grace, staleness, expiry, retention) are **query filters** with a UTC ISO cutoff.

## Corrections to the spec (verified against code and Galène docs on 2026-10-04)

The spec's code is copied verbatim wherever this plan says so, **except** where these corrections apply:

1. **Identity** comes from Plan A's `IAccessTokenVerifier.GetVerifiedUserId(Request.Headers.Authorization)`. Do not create the spec's `CallerIdentityAccessor`, `SupabaseAuthConfig`, or `AddHttpContextAccessor()`: the tokens are `TokenService` tokens (claim `nameid`, `JwtSettings:Secret`), not Supabase tokens.
2. **No profile cache in this feature.** Display names come from the `users` table via the roster (`GetParticipants`), which the join response and the `participant_joined` broadcast both carry. Nothing reads `UserCacheService`.
3. **Entities omit `Schema`.** `TableAttribute` has no `Schema` property; the schema comes from `SupabaseOptions.Schema`.
4. **Unique-violation detection** uses `PostgrestException.Content` (a `string`). The spec's `ex.Response?.Content?.Contains(...)` does not compile.
5. **SFU identity is set inside `JoinEventStream`** as `{videoStreamId}:{userId}` (the Galène username, `sub`), so the spec's `SetSfuIdentity` round trip is removed. The host row created by Start has no identity until the host joins.
6. **`left_reason = 'demoted'` is removed.** Demote revokes publishing via Galène `unpresent` and does not disconnect, so no code path would write it. Spec test L10 becomes `Rejoin_AfterDemotionAndDrop_StaysViewer`.
7. **Founders can join their own stream** even when they are not event attendees; join accepts attendees **or** anyone who may control the event's video.
8. **No custom exceptions in controllers.** `RequireLiveEventStream` in the repository replaces the spec's `?? throw new NotFoundException` in controllers.
9. **`StartVideoStreamForm` is dropped** (empty, "reserved"); start takes no body.
10. **`MaxPresenters` is a parameter** of `PromotePresenter`; `AlSaqr.Data` cannot reference `AlSaqr.Infrastructure.Config`.
11. **Leave, promote, demote and touch return the stream too**, so the controller has `RoomName` without a second lookup.
12. **Start compensates when room creation fails** by ending the just-created stream before rethrowing.
13. **SFU: Galène replaces LiveKit.** Spec §SFU choice, §1.1 (`LiveKitConfig`), §4 (`LiveKitVideoService`) and §11 (LiveKit compose and client snippet) are superseded by Task 3 and Task 10. Mapping: LiveKit room → Galène group `stream-{id}`; `canPublish` grant → token `permissions: ["present"]`; `UpdateParticipant` → operator `present`/`unpresent`; `RemoveParticipant` → operator `kick`; `DeleteRoom` → operator kick-all, then admin `DELETE`; `emptyTimeout` → the reaper (Galène API groups do not self-destruct); `maxParticipants` → `max-clients`; C11 is additionally enforced by the group's `expires` field.
14. **C7 (camera + mic only) cannot be enforced server-side.** Galène's `present` covers every source, including screen share. The presenter cap (C5) still bounds the number of publishers. The AlSaqr client does not offer screen share until PRD P2 is pulled in.
15. **Galène allows two connections to share a username**, so one-seat-per-user is enforced by kicking the older connection on a repeat join (`HadLiveConnection`).
16. **Token refresh** is a new route, `POST event/{eventId}/token`, so a slow first connect or a long reconnect never needs a full re-join.
17. **The reaper is split** into `IVideoStreamReaper` (logic, explicit `utcNow`, tested) and a thin `VideoStreamReaperService` loop.
18. **DI lives in `AddEventVideo(...)`**, with startup validation of the Galène config, so a test can build and validate the graph and a bad deploy fails at boot.
19. **Per-test isolation is `TRUNCATE`**, not transaction rollback (Plan A correction 3).

## Review Focus

1. **The SFU is down when Start is pressed** → 500, and the event has **no** live stream afterwards, so a retry can succeed. Pinned in Task 9 (`Start_WhenCreateRoomFails_Returns500_AndLeavesNoLiveStream`).
2. **A group founder who is not an event attendee starts the stream, then joins it** → join succeeds as host. Pinned in Task 6 (`Join_FounderWhoIsNotAnAttendee_JoinsAsHost`).
3. **Join again from a second tab or after a reload** → same row, same role, and the older SFU connection is kicked. Pinned in Task 6 (`SecondDevice_DoesNotCreateSecondRow_AndReportsLiveConnection`) and Task 9 (`Join_Again_KicksOlderConnection`).
4. **A client heartbeats or refreshes its token after the reaper removed it** → 403 "Join again", so the client's reconnect logic knows to re-join. Pinned in Task 6 (`Heartbeat_AfterReaped_ThrowsForbidden`) and Task 9 (`Token_WhenNotLive_Returns403`).
5. **A demoted presenter replays their old presenter token** → only possible within the 90-second TTL, and the token is room-scoped. Pinned in Task 3 (`Token_IsScopedToOneRoom_AndLivesAtMost90Seconds`, `TokenForAnotherRoom_IsRefused`). Accepted residual risk: a reconnect inside that window regains `present` until the host demotes again.

Accepted limitation (not a test): two simultaneous promotes can exceed `MaxPresenters` by one. The cap is a cost guard, not a security boundary.

## Client contract (perceived performance)

The backend supports these client behaviors; the frontend implements them. They are part of this feature's acceptance.

**Connection state machine.** States: `joining` → `connected` ⇄ `reconnecting` → `left`.

| Trigger | Client action | Shown to user |
|---|---|---|
| User presses Join | Start `getUserMedia` (presenters) **and** `POST …/join` in parallel; render the roster from the join response | "Joining…" immediately, local camera preview as soon as it opens |
| Galène WebSocket drops, or heartbeat fails with a network error | Reconnect to Galène with the current token; retry with backoff (1 s, 2 s, 4 s, then every 5 s) | "Reconnecting…" after 1 s, never an error page |
| Galène refuses the token (expired), or reconnecting outlasts the token's `expiresAt` | `POST …/token`, then reconnect with the fresh token | Still "Reconnecting…" |
| `heartbeat` or `token` answers **403** | The reaper removed us: `POST …/join` again. Within 5 minutes this restores the previous role | Still "Reconnecting…" |
| `join` answers 404 | The stream ended | "The call has ended" |
| `stream_ended` broadcast | Disconnect | "The call has ended" |

**Optimistic UI.**
- **Leave is instant:** tear down the UI and the Galène connection first, then `fetch(…/leave, { method: 'POST', keepalive: true, headers: { Authorization } })`. Use `keepalive`, not `navigator.sendBeacon`: `sendBeacon` cannot send the `Authorization` header, so the leave would be rejected with 401.
- **Roster updates without refetching:** `participant_joined` carries the full roster entry (username, avatar, role); `participant_left` and `role_changed` carry ids. Apply them to local state.
- **Promote/demote:** show the new role immediately on the host's screen; roll back if the call returns non-2xx. The target's permission changes server-side through Galène; no rejoin is needed.
- **Hide the operator:** the backend joins groups briefly as `alsaqr-operator` to moderate. The client never renders that username.
- **Heartbeat** every 20 s (well under the 60 s reaper timeout). The join UI states: *"This call is not recorded. We keep a record of who joined and for how long for 90 days."*

---

## File Structure

| Path | Responsibility |
|---|---|
| `AlSaqr.Data/Entities/Meetup/sql/video_streams.sql` | DDL for both tables |
| `AlSaqr.Data/Entities/Meetup/VideoStream.cs`, `VideoParticipant.cs` | Entities |
| `AlSaqr.Domain/Meetup/EventVideo.cs` | Client-contract DTOs |
| `AlSaqr.Infrastructure/Config/GaleneConfig.cs` | Galène settings |
| `AlSaqr.Infrastructure/Video/IVideoSfuService.cs` | SFU-neutral control-plane interface |
| `AlSaqr.Infrastructure/Video/GaleneTokens.cs` | HS256 room-token signing (shared) |
| `AlSaqr.Infrastructure/Video/GaleneOperator.cs` | Hidden-operator WebSocket client (present/unpresent/kick) |
| `AlSaqr.Infrastructure/Video/GaleneVideoService.cs` | `IVideoSfuService` over the admin API + operator |
| `AlSaqr.Infrastructure/Video/SupabaseBroadcastClient.cs` | Channel-agnostic realtime broadcast POST |
| `AlSaqr.Infrastructure/Video/EventVideoBroadcaster.cs` | Video lifecycle events |
| `AlSaqr.Data/Repositories/Meetup/Impl/IVideoStreamRepository.cs` | Repository interface (this repo puts interfaces in `Impl/`) |
| `AlSaqr.Data/Repositories/Meetup/VideoStreamRepository.cs` | All authorization and persistence |
| `AlSaqr.API/HostedServices/VideoStreamReaper.cs` | Reap/expire/purge logic |
| `AlSaqr.API/HostedServices/VideoStreamReaperService.cs` | 30 s loop + daily purge |
| `AlSaqr.API/Controllers/Meetup/EventVideoController.cs` | 10 routes |
| `AlSaqr.API/Extensions/EventVideoServiceCollectionExtensions.cs` | `AddEventVideo` |
| `docker/galene/Dockerfile`, `docker/galene/dev/config.json` | Galène image (no official one is published) and local config |
| `docker-compose.galene.yml` | Local Galène |
| `tests/AlSaqr.Tests/Video/*` | Fakes, scenario builder, Galène test client, all video tests |

---

### Task 1: Tables and entities

**Files:**
- Create: `AlSaqr.Data/Entities/Meetup/sql/video_streams.sql`
- Create: `AlSaqr.Data/Entities/Meetup/VideoStream.cs`
- Create: `AlSaqr.Data/Entities/Meetup/VideoParticipant.cs`
- Modify: `tests/AlSaqr.Tests/Support/PostgrestFixture.cs` (`RepoSqlFiles`)
- Test: `tests/AlSaqr.Tests/Video/VideoSchemaTests.cs`

**Interfaces:**
- Produces: `VideoStream { Guid Id; Guid EventId; string RoomName; Guid HostId; DateTime StartedAt; DateTime? EndedAt; static string RoomNameFor(Guid videoStreamId) }`
- Produces: `VideoParticipant` with constants `RoleHost/RolePresenter/RoleViewer`, `LeftExplicit/LeftReaped/LeftEnded/LeftExpired`, properties per spec §2, plus `static string SfuIdentityFor(Guid videoStreamId, Guid userId) => $"{videoStreamId}:{userId}"`.

- [ ] **Step 1: Write the failing schema tests**

`tests/AlSaqr.Tests/Video/VideoSchemaTests.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Tests.Support;
using Npgsql;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoSchemaTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;

        public VideoSchemaTests(PostgrestFixture fx) => _fx = fx;

        public Task InitializeAsync() => _fx.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task<(Guid EventId, Guid HostId)> OnlineEvent()
        {
            var host = await _fx.Seed.User("host");
            var group = await _fx.Seed.Group(host);
            return (await _fx.Seed.Event(group, isOnline: true), host);
        }

        private Task<Guid> InsertStream(Guid eventId, Guid hostId, bool ended) =>
            _fx.Seed.Scalar<Guid>(
                $"insert into {S}.video_streams (event_id, room_name, host_id, ended_at) " +
                "values (@event, @room, @host, case when @ended then now() else null end) returning id",
                ("event", eventId), ("room", $"stream-{Guid.NewGuid()}"), ("host", hostId), ("ended", ended));

        [Fact]
        public async Task SecondLiveStreamForSameEvent_IsRejectedByDatabase()
        {
            var (eventId, host) = await OnlineEvent();
            await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertStream(eventId, host, ended: false));
            Assert.Equal("23505", ex.SqlState);
        }

        [Fact]
        public async Task EndedStreams_DoNotBlockANewLiveStream()
        {
            var (eventId, host) = await OnlineEvent();
            await InsertStream(eventId, host, ended: true);

            await InsertStream(eventId, host, ended: false);   // must not throw
        }

        [Fact]
        public async Task LeftAtWithoutLeftReason_IsRejected()
        {
            var (eventId, host) = await OnlineEvent();
            var stream = await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"insert into {S}.video_participants (event_id, participant_id, video_stream_id, left_at) " +
                "values (@event, @user, @stream, now())",
                ("event", eventId), ("user", host), ("stream", stream)));
            Assert.Equal("23514", ex.SqlState);
        }

        [Fact]
        public async Task DemotedIsNotAValidLeftReason()
        {
            var (eventId, host) = await OnlineEvent();
            var stream = await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"insert into {S}.video_participants (event_id, participant_id, video_stream_id, left_at, left_reason) " +
                "values (@event, @user, @stream, now(), 'demoted')",
                ("event", eventId), ("user", host), ("stream", stream)));
            Assert.Equal("23514", ex.SqlState);
        }

        [Fact]
        public async Task ParticipantEventId_MustMatchItsStream()
        {
            var (eventId, host) = await OnlineEvent();
            var (otherEvent, _) = await OnlineEvent();
            var stream = await InsertStream(eventId, host, ended: false);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => _fx.Seed.Execute(
                $"insert into {S}.video_participants (event_id, participant_id, video_stream_id) " +
                "values (@event, @user, @stream)",
                ("event", otherEvent), ("user", host), ("stream", stream)));
            Assert.Equal("23503", ex.SqlState);
        }

        [Fact]
        public async Task VideoStreamEntity_RoundTripsThroughPostgrest()
        {
            var (eventId, host) = await OnlineEvent();
            var id = Guid.NewGuid();

            await _fx.Client.From<VideoStream>().Insert(
                new VideoStream { Id = id, EventId = eventId, HostId = host, RoomName = VideoStream.RoomNameFor(id) },
                new Supabase.Postgrest.QueryOptions { Returning = ReturnType.Minimal });

            var loaded = await _fx.Client.From<VideoStream>()
                .Filter("id", Operator.Equals, id.ToString())
                .Single();

            Assert.Equal($"stream-{id}", loaded!.RoomName);
            Assert.Null(loaded.EndedAt);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoSchemaTests"`
Expected: build FAIL — `The type or namespace name 'VideoStream' could not be found`.

- [ ] **Step 3: Create the DDL**

Create `AlSaqr.Data/Entities/Meetup/sql/video_streams.sql` with the **exact SQL block from spec §2** (the one beginning `-- AlSaqr.Data/Entities/Meetup/sql/video_streams.sql`), with one change (correction 6): in `video_participants_left_reason_check`, delete the line

```sql
        'demoted'::character varying,
```

so the allowed values are `explicit`, `reaped`, `ended`, `expired`.

- [ ] **Step 4: Create the entities**

Create `AlSaqr.Data/Entities/Meetup/VideoStream.cs` with the **exact C# block from spec §2** (`// AlSaqr.Data/Entities/Meetup/VideoStream.cs`).

Create `AlSaqr.Data/Entities/Meetup/VideoParticipant.cs` with the **exact C# block from spec §2** (`// AlSaqr.Data/Entities/Meetup/VideoParticipant.cs`), with two changes:

1. Delete the line `public const string LeftDemoted = "demoted";` (correction 6).
2. Add, after the `LeftExpired` constant:

```csharp
        /// <summary>
        /// The SFU username for a user on a stream. Stream-scoped like the room name,
        /// so a token minted for an earlier stream cannot be replayed (spec §4).
        /// </summary>
        public static string SfuIdentityFor(Guid videoStreamId, Guid userId) => $"{videoStreamId}:{userId}";
```

Also, in the `SfuIdentity` doc comment, replace `set while connected` with `set on join, cleared on leave`.

- [ ] **Step 5: Register the DDL with the fixture**

In `tests/AlSaqr.Tests/Support/PostgrestFixture.cs`, change `RepoSqlFiles` to:

```csharp
        public static readonly string[] RepoSqlFiles =
        {
            "AlSaqr.Data/Entities/Meetup/sql/event_attendees.sql",
            "AlSaqr.Data/Entities/Meetup/sql/video_streams.sql",
        };
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoSchemaTests"`
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add AlSaqr.Data/Entities/Meetup/sql/video_streams.sql AlSaqr.Data/Entities/Meetup/VideoStream.cs AlSaqr.Data/Entities/Meetup/VideoParticipant.cs tests/AlSaqr.Tests
git commit -m "feat(video): add video_streams/video_participants tables and entities"
```

---

### Task 2: Client-contract DTOs

**Files:**
- Create: `AlSaqr.Domain/Meetup/EventVideo.cs`
- Test: `tests/AlSaqr.Tests/Video/EventVideoContractTests.cs`

**Interfaces:**
- Produces: `AlSaqr.Domain.Meetup.EventVideo.{VideoStreamToDisplay, VideoParticipantDto, JoinVideoStreamResultDto, VideoTokenDto, CameraStateForm}`, field names exactly as spec §3 plus `group` and `VideoTokenDto`.

- [ ] **Step 1: Write the failing contract test (spec C2)**

`tests/AlSaqr.Tests/Video/EventVideoContractTests.cs`:

```csharp
using System.Text.Json;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Tests.Video
{
    public class EventVideoContractTests
    {
        private static HashSet<string> Keys(JsonElement element) =>
            element.EnumerateObject().Select(p => p.Name).ToHashSet();

        [Fact]
        public void JoinResult_SerializesToContractShape()
        {
            var dto = new JoinVideoStreamResultDto
            {
                Stream = new VideoStreamToDisplay { VideoStreamId = Guid.NewGuid() },
                Participants = { new VideoParticipantDto { Username = "a" } },
            };

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));
            var root = doc.RootElement;

            Assert.Equal(
                new HashSet<string> { "stream", "role", "wsUrl", "group", "token", "expiresAt", "canPublish", "participants" },
                Keys(root));
            Assert.Equal(
                new HashSet<string>
                {
                    "videoStreamId", "eventId", "eventName", "hostId", "hostUsername", "hostAvatar",
                    "startedAt", "endedAt", "participantCount", "isLive",
                },
                Keys(root.GetProperty("stream")));
            Assert.Equal(
                new HashSet<string> { "participantId", "username", "avatar", "role", "cameraEnabled", "muted" },
                Keys(root.GetProperty("participants")[0]));
        }

        [Fact]
        public void VideoToken_SerializesToContractShape()
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new VideoTokenDto()));

            Assert.Equal(
                new HashSet<string> { "role", "wsUrl", "group", "token", "expiresAt", "canPublish" },
                Keys(doc.RootElement));
        }

        [Fact]
        public void CameraStateForm_BindsFromCamelCaseValues()
        {
            var form = JsonSerializer.Deserialize<CameraStateForm>(
                "{\"cameraEnabled\":true,\"muted\":false}",
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.True(form!.CameraEnabled);
            Assert.False(form.Muted);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoContractTests"`
Expected: build FAIL — `The type or namespace name 'EventVideo' does not exist in the namespace 'AlSaqr.Domain.Meetup'`.

- [ ] **Step 3: Implement**

Create `AlSaqr.Domain/Meetup/EventVideo.cs` with the **exact C# block from spec §3**, with three changes:

1. Remove the `StartVideoStreamForm` class and its doc comment (correction 9). Keep `CameraStateForm`.
2. In `JoinVideoStreamResultDto`'s doc comment, replace `wsUrl + token are the ONLY LiveKit values that leave the API` with `wsUrl, group and token are the ONLY SFU values that leave the API`.
3. In `JoinVideoStreamResultDto`, add after `WsUrl`:

```csharp
            /// <summary>Galène group (room) name the client joins with the token.</summary>
            [JsonPropertyName("group")]
            public string Group { get; set; } = string.Empty;
```

4. Add, after `JoinVideoStreamResultDto`, the token-refresh response (correction 16):

```csharp
        /// <summary>
        /// A fresh room token for a participant who is already live: used when the
        /// first connect is slow or a reconnect outlasts the 90 s token. Carries no
        /// roster and triggers no broadcast.
        /// </summary>
        public class VideoTokenDto
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("wsUrl")]
            public string WsUrl { get; set; } = string.Empty;

            [JsonPropertyName("group")]
            public string Group { get; set; } = string.Empty;

            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("expiresAt")]
            public DateTime ExpiresAt { get; set; }

            [JsonPropertyName("canPublish")]
            public bool CanPublish { get; set; }
        }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoContractTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add AlSaqr.Domain/Meetup/EventVideo.cs tests/AlSaqr.Tests/Video/EventVideoContractTests.cs
git commit -m "feat(video): add event video DTOs"
```

---

### Task 3: Galène control plane (go/no-go spike first)

This task proves Galène can do everything the spec's permission model needs **before** anything else depends on it. The gate tests (marked **GATE**) run against a real Galène container. If any gate test fails and cannot be fixed within one working day, **stop**: report which assumption failed, and re-plan this task on Janus behind the same `IVideoSfuService` interface. Tasks 1, 2, and 4–7 do not depend on the SFU and stay valid either way.

**Files:**
- Create: `docker/galene/Dockerfile`
- Create: `docker/galene/dev/config.json`
- Create: `AlSaqr.Infrastructure/Config/GaleneConfig.cs`
- Create: `AlSaqr.Infrastructure/Video/IVideoSfuService.cs`
- Create: `AlSaqr.Infrastructure/Video/GaleneTokens.cs`
- Create: `AlSaqr.Infrastructure/Video/GaleneOperator.cs`
- Create: `AlSaqr.Infrastructure/Video/GaleneVideoService.cs`
- Create: `tests/AlSaqr.Tests/Support/RecordingHandler.cs`
- Create: `tests/AlSaqr.Tests/Support/JwtJson.cs`
- Create: `tests/AlSaqr.Tests/Support/GaleneContainer.cs`
- Create: `tests/AlSaqr.Tests/Video/GaleneTestClient.cs`
- Test: `tests/AlSaqr.Tests/Video/GaleneVideoServiceTests.cs`
- Test: `tests/AlSaqr.Tests/Video/GaleneServerTests.cs`

**Interfaces:**
- Produces: `GaleneConfig { AdminUrl; PublicUrl; AdminUsername; AdminPassword; TokenSecret; MaxParticipants = 50; MaxPresenters = 9 }`
- Produces: `record SfuJoinToken(string Token, string WsUrl, string Group, DateTime ExpiresAt)` and
  ```csharp
  public interface IVideoSfuService
  {
      Task CreateRoomAsync(string roomName, int maxParticipants, DateTime notAfterUtc, CancellationToken ct = default);
      SfuJoinToken MintJoinToken(string roomName, string identity, bool canPublish);
      Task SetPublishAsync(string roomName, string identity, bool canPublish, CancellationToken ct = default);
      Task RemoveParticipantsAsync(string roomName, IReadOnlyCollection<string> identities, CancellationToken ct = default);
      Task DeleteRoomAsync(string roomName, CancellationToken ct = default);
  }
  ```
- Produces: `IGaleneOperator { Task<bool> ActAsync(string roomName, string kind, IReadOnlyCollection<string>? usernames, CancellationToken ct = default); }` (`kind` is `present` | `unpresent` | `kick`; `usernames == null` means everyone but the operator; returns `false` when Galène refuses the operator's join).
- Produces (static helpers): `GaleneTokens.Write(GaleneConfig, string roomName, string username, IReadOnlyCollection<string> permissions, TimeSpan ttl, out DateTime expiresAt)`, `GaleneTokens.Audience(GaleneConfig, string roomName)`, `GaleneTokens.WebSocketUrl(string baseUrl)`, `GaleneTokens.KeyId == "alsaqr"`.
- Produces (test support, used by Task 4): `RecordingHandler` with `List<RecordedRequest> Requests`, `HttpStatusCode Status`, `Func<RecordedRequest, HttpStatusCode>? Respond`, `string ResponseBody`; `record RecordedRequest(HttpMethod Method, string Url, IReadOnlyDictionary<string, string> Headers, string Body)`.

- [ ] **Step 1: Build a pinned Galène image**

No official Galène image is published, so build one from the release tag.

`docker/galene/Dockerfile`:

```dockerfile
# Galène SFU for AlSaqr event video (docs/superpowers/plans/2026-10-04-event-video-streaming.md).
# No official image is published, so this builds a pinned release from source.
FROM golang:1.24-alpine AS build
ARG GALENE_VERSION=galene-1.2.1
RUN apk add --no-cache git \
 && git clone --depth 1 --branch "${GALENE_VERSION}" https://github.com/jech/galene.git /src
WORKDIR /src
RUN CGO_ENABLED=0 go build -ldflags="-s -w" -o /out/galene .

FROM alpine:3.21
COPY --from=build /out/galene /usr/local/bin/galene
COPY --from=build /src/static /srv/galene/static
RUN mkdir -p /srv/galene/data /srv/galene/groups
WORKDIR /srv/galene
# 8443: HTTP(S) + WebSocket + admin API. 1194: built-in TURN. Media uses an ephemeral UDP range.
EXPOSE 8443 1194/tcp 1194/udp
ENTRYPOINT ["galene", "-data", "/srv/galene/data", "-groups", "/srv/galene/groups", "-static", "/srv/galene/static"]
```

`docker/galene/dev/config.json` (local development only; production mounts its own):

```json
{
  "writableGroups": true,
  "users": {
    "alsaqr-admin": { "password": "alsaqr-local-galene-admin", "permissions": "admin" }
  }
}
```

Run: `docker build -t alsaqr-galene:spike docker/galene; docker run --rm alsaqr-galene:spike -help`
Expected: the build succeeds and the help output lists `-insecure`, `-http`, `-data`, `-groups`, `-static`, `-turn`, and `-udp-range`.

- If the clone fails, the tag name differs: list tags with `git ls-remote --tags https://github.com/jech/galene.git` and pin the `1.2.1` tag that exists.
- If `go build` reports a newer Go requirement, bump the `golang:` image to the version `go.mod` names.
- If a flag name differs, use the real name in the Dockerfile and in `GaleneContainer` (Step 2).

- [ ] **Step 2: Write the test support**

`tests/AlSaqr.Tests/Support/RecordingHandler.cs`:

```csharp
using System.Net;

namespace AlSaqr.Tests.Support
{
    public sealed record RecordedRequest(
        HttpMethod Method, string Url, IReadOnlyDictionary<string, string> Headers, string Body);

    /// <summary>
    /// Stands in for an external HTTP API (Galène admin API, Supabase Realtime).
    /// Records every request and answers with <see cref="Respond"/> when set,
    /// otherwise with <see cref="Status"/>.
    /// </summary>
    public sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = new();

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public Func<RecordedRequest, HttpStatusCode>? Respond { get; set; }

        public string ResponseBody { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers
                .Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, request.RequestUri!.ToString(), headers, body);
            Requests.Add(recorded);

            return new HttpResponseMessage(Respond?.Invoke(recorded) ?? Status)
            {
                Content = new StringContent(ResponseBody),
            };
        }
    }
}
```

`tests/AlSaqr.Tests/Support/JwtJson.cs`:

```csharp
using System.Text;
using System.Text.Json;

namespace AlSaqr.Tests.Support
{
    /// <summary>Decodes a JWT segment as raw JSON, so claims are asserted exactly as sent.</summary>
    public static class JwtJson
    {
        public static JsonElement Header(string jwt) => Segment(jwt, 0);

        public static JsonElement Payload(string jwt) => Segment(jwt, 1);

        private static JsonElement Segment(string jwt, int index)
        {
            var segment = jwt.Split('.')[index].Replace('-', '+').Replace('_', '/');
            segment = segment.PadRight(segment.Length + (4 - segment.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(segment)));
            return doc.RootElement.Clone();
        }
    }
}
```

`tests/AlSaqr.Tests/Support/GaleneContainer.cs`:

```csharp
using System.Text;
using AlSaqr.Infrastructure.Config;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace AlSaqr.Tests.Support
{
    /// <summary>
    /// A throwaway Galène server built from docker/galene. One per test class;
    /// every test uses its own room name, so tests cannot see each other.
    /// </summary>
    public sealed class GaleneContainer : IAsyncLifetime
    {
        public const string AdminUser = "alsaqr-admin";
        public const string AdminPassword = "test-galene-admin-password";
        public const string TokenSecret = "test-galene-token-secret-0123456789-abcdef";

        private const string ConfigJson =
            "{\"writableGroups\":true,\"users\":{\"" + AdminUser + "\":" +
            "{\"password\":\"" + AdminPassword + "\",\"permissions\":\"admin\"}}}";

        private readonly IFutureDockerImage _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "docker/galene")
            .WithDockerfile("Dockerfile")
            .WithName("alsaqr-galene:test")
            .WithCleanUp(false)   // the Go build is the slow part; keep the image between runs
            .Build();

        private IContainer _container = default!;

        public string BaseUrl { get; private set; } = default!;

        public GaleneConfig Config { get; private set; } = default!;

        public async Task InitializeAsync()
        {
            await _image.CreateAsync();

            _container = new ContainerBuilder()
                .WithImage(_image)
                .WithResourceMapping(Encoding.UTF8.GetBytes(ConfigJson), "/srv/galene/data/config.json")
                // Plain HTTP for tests; no TURN (containers reach each other directly).
                .WithCommand("-insecure", "-http", ":8443", "-turn", "")
                .WithPortBinding(8443, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(8443).ForPath("/")))
                .Build();

            await _container.StartAsync();

            BaseUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8443)}";
            Config = new GaleneConfig
            {
                AdminUrl = BaseUrl,
                PublicUrl = BaseUrl,
                AdminUsername = AdminUser,
                AdminPassword = AdminPassword,
                TokenSecret = TokenSecret,
            };
        }

        public async Task DisposeAsync() => await _container.DisposeAsync();
    }
}
```

`tests/AlSaqr.Tests/Video/GaleneTestClient.cs`:

```csharp
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
```

- [ ] **Step 3: Write the failing unit tests**

`tests/AlSaqr.Tests/Video/GaleneVideoServiceTests.cs`:

```csharp
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
        private const string Secret = "test-galene-token-secret-0123456789-abcdef";
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
            var token = _service.MintJoinToken("stream-a", "s:u", canPublish: true).Token;

            Assert.Equal(new[] { "present" }, Permissions(token));
            Assert.Equal("https://video.test/group/stream-a/", JwtJson.Payload(token).GetProperty("aud").GetString());
            Assert.Equal("s:u", JwtJson.Payload(token).GetProperty("sub").GetString());
        }

        [Fact]
        public void ViewerToken_CannotPresent()
        {
            var token = _service.MintJoinToken("stream-a", "s:u", canPublish: false).Token;

            Assert.DoesNotContain("present", Permissions(token));
            Assert.DoesNotContain("op", Permissions(token));
            Assert.DoesNotContain("record", Permissions(token));
        }

        [Fact]
        public void Token_IsScopedToOneRoom_AndLivesAtMost90Seconds()
        {
            var token = _service.MintJoinToken("stream-a", "s:u", canPublish: false).Token;

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
            var token = _service.MintJoinToken("stream-a", "s:u", canPublish: false);

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
            await _service.SetPublishAsync("stream-a", "s:u", canPublish: true);
            await _service.SetPublishAsync("stream-a", "s:u", canPublish: false);

            Assert.Equal(new[] { "present stream-a s:u", "unpresent stream-a s:u" }, _operator.Calls);
        }

        [Fact]
        public async Task SetPublish_WhenOperatorRefused_Throws()
        {
            // A silent no-op here would leave a demoted user publishing.
            _operator.Accept = false;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SetPublishAsync("stream-a", "s:u", canPublish: false));
        }

        [Fact]
        public async Task RemoveParticipants_KicksOnlyGivenIdentities_AndSkipsEmpty()
        {
            await _service.RemoveParticipantsAsync("stream-a", Array.Empty<string>());
            await _service.RemoveParticipantsAsync("stream-a", new[] { "s:1", "s:2" });

            Assert.Equal(new[] { "kick stream-a s:1,s:2" }, _operator.Calls);
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

            public Task<bool> ActAsync(
                string roomName, string kind, IReadOnlyCollection<string>? usernames, CancellationToken ct = default)
            {
                Calls.Add($"{kind} {roomName} {(usernames == null ? "*" : string.Join(",", usernames))}");
                return Task.FromResult(Accept);
            }
        }
    }
}
```

- [ ] **Step 4: Write the failing gate tests (real Galène)**

`tests/AlSaqr.Tests/Video/GaleneServerTests.cs`:

```csharp
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

            var (client, permissions) = await Join(room, "s:presenter", canPublish: true);
            await using var _ = client;

            Assert.Contains("present", permissions);
        }

        [Fact]
        public async Task ViewerToken_JoinsWithoutPresentPermission()
        {
            var room = await NewRoom();

            var (client, permissions) = await Join(room, "s:viewer", canPublish: false);
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
                TokenSecret = "some-other-secret-0123456789-abcdefghijklmn",
            };
            var forged = GaleneTokens.Write(forger, room, "s:intruder", new[] { "present" }, TimeSpan.FromSeconds(90), out _);

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
            var joined = await client.JoinAsync(roomA, _service.MintJoinToken(roomB, "s:u", true).Token);

            Assert.Equal("fail", GaleneTestClient.Kind(joined));
        }

        [Fact] // GATE: server-side demote
        public async Task Unpresent_RevokesPublishOnLiveConnection()
        {
            var room = await NewRoom();
            var (client, _) = await Join(room, "s:presenter", canPublish: true);
            await using var _c = client;

            await _service.SetPublishAsync(room, "s:presenter", canPublish: false);

            var change = await client.WaitForAsync(
                m => GaleneTestClient.Type(m) == "joined" && GaleneTestClient.Kind(m) == "change", Wait);
            Assert.DoesNotContain("present", GaleneTestClient.Permissions(change));
        }

        [Fact]
        public async Task Present_GrantsPublishOnLiveConnection()
        {
            var room = await NewRoom();
            var (client, _) = await Join(room, "s:viewer", canPublish: false);
            await using var _c = client;

            await _service.SetPublishAsync(room, "s:viewer", canPublish: true);

            var change = await client.WaitForAsync(
                m => GaleneTestClient.Type(m) == "joined" && GaleneTestClient.Kind(m) == "change", Wait);
            Assert.Contains("present", GaleneTestClient.Permissions(change));
        }

        [Fact] // GATE: server-side kick
        public async Task Kick_DisconnectsOnlyTheTarget()
        {
            var room = await NewRoom();
            var (target, _) = await Join(room, "s:target", canPublish: false);
            var (bystander, _) = await Join(room, "s:bystander", canPublish: false);
            await using var _t = target;
            await using var _b = bystander;

            await _service.RemoveParticipantsAsync(room, new[] { "s:target" });

            Assert.True(await target.WaitForLeaveAsync(Wait));
            Assert.False(await bystander.WaitForLeaveAsync(TimeSpan.FromSeconds(1)));
        }

        [Fact] // GATE: server-side end
        public async Task DeleteRoom_DisconnectsEveryone_AndRefusesNewJoins()
        {
            var room = await NewRoom();
            var token = _service.MintJoinToken(room, "s:late", false).Token;
            var (client, _) = await Join(room, "s:u", canPublish: true);
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
            var (first, _) = await Join(room, "s:1", false);
            var (second, _) = await Join(room, "s:2", false);
            await using var _1 = first;
            await using var _2 = second;

            await using var third = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);
            var joined = await third.JoinAsync(room, _service.MintJoinToken(room, "s:3", false).Token);

            Assert.Equal("fail", GaleneTestClient.Kind(joined));
        }

        [Fact]
        public async Task DuplicateUsername_KeepsBothConnections()
        {
            // Documents correction 15. If Galène ever starts replacing the older
            // connection itself, this fails: then remove the HadLiveConnection kick.
            var room = await NewRoom();
            var (older, _) = await Join(room, "s:same", false);
            var (newer, _) = await Join(room, "s:same", false);
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
            var token = GaleneTokens.Write(publicView, room, "s:u", Array.Empty<string>(), TimeSpan.FromSeconds(90), out _);

            await using var client = await GaleneTestClient.ConnectAsync(_galene.BaseUrl);

            Assert.Equal("join", GaleneTestClient.Kind(await client.JoinAsync(room, token)));
        }
    }
}
```

- [ ] **Step 5: Run to verify they fail**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~Galene"`
Expected: build FAIL — `The type or namespace name 'GaleneVideoService' could not be found`.

- [ ] **Step 6: Implement the configuration and interface**

`AlSaqr.Infrastructure/Config/GaleneConfig.cs`:

```csharp
namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// Galène (self-hosted, MIT SFU) settings, bound from the "Galene" configuration
    /// section. AdminPassword and TokenSecret are server-side only: they must never
    /// appear in a response, log line, or client-visible error
    /// (docs/superpowers/plans/2026-10-04-event-video-streaming.md).
    /// </summary>
    public sealed class GaleneConfig
    {
        /// <summary>Server-side base for the admin API and operator WebSocket, e.g. http://galene.internal:8443</summary>
        public string AdminUrl { get; set; } = "http://localhost:8443";

        /// <summary>Public origin browsers connect to; also the token audience host, e.g. https://video.alsaqr.app</summary>
        public string PublicUrl { get; set; } = "http://localhost:8443";

        public string AdminUsername { get; set; } = default!;

        public string AdminPassword { get; set; } = default!;

        /// <summary>HS256 room-token secret (≥32 chars), installed into every room's keys.</summary>
        public string TokenSecret { get; set; } = default!;

        /// <summary>Total room occupancy cap (C12) — Galène max-clients.</summary>
        public int MaxParticipants { get; set; } = 50;

        /// <summary>Concurrent publisher cap, host included (C5).</summary>
        public int MaxPresenters { get; set; } = 9;
    }
}
```

`AlSaqr.Infrastructure/Video/IVideoSfuService.cs`:

```csharp
namespace AlSaqr.Infrastructure.Video
{
    /// <summary>A minted room token, where to connect, and when the token stops working.</summary>
    public sealed record SfuJoinToken(string Token, string WsUrl, string Group, DateTime ExpiresAt);

    /// <summary>
    /// SFU control plane for event video. The browser only ever receives a
    /// SfuJoinToken; admin credentials and the signing secret stay behind this
    /// interface. No recording, egress, or transcription operation exists here,
    /// and none may be added (C10).
    /// </summary>
    public interface IVideoSfuService
    {
        /// <summary>
        /// Creates the room with the cost guards: occupancy cap (C12), recording off
        /// (C10), and an access cutoff at notAfterUtc (C11, backing the reaper).
        /// </summary>
        Task CreateRoomAsync(string roomName, int maxParticipants, DateTime notAfterUtc, CancellationToken ct = default);

        /// <summary>
        /// A short-lived (C8), room-scoped token. canPublish is half the permission
        /// model; SetPublishAsync changes it on a live connection.
        /// </summary>
        SfuJoinToken MintJoinToken(string roomName, string identity, bool canPublish);

        /// <summary>Grants or revokes publishing on a live connection, server-side.</summary>
        Task SetPublishAsync(string roomName, string identity, bool canPublish, CancellationToken ct = default);

        /// <summary>Disconnects these identities. Best effort: someone already gone is not an error.</summary>
        Task RemoveParticipantsAsync(string roomName, IReadOnlyCollection<string> identities, CancellationToken ct = default);

        /// <summary>Disconnects everyone and deletes the room. Best effort for a room already gone.</summary>
        Task DeleteRoomAsync(string roomName, CancellationToken ct = default);
    }
}
```

- [ ] **Step 7: Implement token signing**

`AlSaqr.Infrastructure/Video/GaleneTokens.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AlSaqr.Infrastructure.Config;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// Galène third-party tokens: HS256 JWTs with sub (username), aud (the group
    /// URL), permissions (string array), iat and exp. Galène verifies them against
    /// the room's installed key, matched by kid.
    /// </summary>
    public static class GaleneTokens
    {
        public const string KeyId = "alsaqr";

        public static string Write(
            GaleneConfig config,
            string roomName,
            string username,
            IReadOnlyCollection<string> permissions,
            TimeSpan ttl,
            out DateTime expiresAt)
        {
            var now = DateTimeOffset.UtcNow;
            var exp = now.Add(ttl);
            expiresAt = exp.UtcDateTime;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.TokenSecret)) { KeyId = KeyId };
            var header = new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            var payload = new JwtPayload
            {
                { "sub", username },
                { "aud", Audience(config, roomName) },
                { "permissions", permissions.ToArray() },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", exp.ToUnixTimeSeconds() },
            };

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        }

        /// <summary>Galène matches aud against "/group/{name}/" on the public host.</summary>
        public static string Audience(GaleneConfig config, string roomName) =>
            $"{config.PublicUrl.TrimEnd('/')}/group/{roomName}/";

        /// <summary>The JWK "k" value for the room key: base64url of the secret's bytes.</summary>
        public static string KeyValue(GaleneConfig config) =>
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(config.TokenSecret));

        /// <summary>http(s)://host → ws(s)://host/ws, Galène's WebSocket endpoint.</summary>
        public static string WebSocketUrl(string baseUrl)
        {
            var uri = new UriBuilder(baseUrl.TrimEnd('/'));
            uri.Scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            uri.Path = uri.Path.TrimEnd('/') + "/ws";
            return uri.Uri.ToString();
        }
    }
}
```

Note: `UriBuilder` drops the default port (`https` → 443) when converting, which gives `wss://video.test/ws`. Non-default ports (`:8443`) are kept.

- [ ] **Step 8: Implement the operator**

`AlSaqr.Infrastructure/Video/GaleneOperator.cs`:

```csharp
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
        /// <param name="kind">"present", "unpresent" or "kick".</param>
        /// <param name="usernames">Targets by Galène username; null means everyone but the operator.</param>
        /// <returns>false when Galène refused the operator's join (typically: the group no longer exists).</returns>
        Task<bool> ActAsync(string roomName, string kind, IReadOnlyCollection<string>? usernames, CancellationToken ct = default);
    }

    public sealed class GaleneOperator : IGaleneOperator
    {
        public const string Username = "alsaqr-operator";

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

        public async Task<bool> ActAsync(
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
                        return false;
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
                return false;
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
            return true;
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
```

- [ ] **Step 9: Implement the service**

`AlSaqr.Infrastructure/Video/GaleneVideoService.cs`:

```csharp
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
```

- [ ] **Step 10: Run the unit tests**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~GaleneVideoServiceTests"`
Expected: PASS (11 tests).

- [ ] **Step 11: Run the gate tests and decide**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~GaleneServerTests"`
Expected: PASS (11 tests). The first run builds the image, which takes a few minutes.

Known adjustments. Each one is a one-line change; make it, re-run, and record it in the commit message:

| Failing test | Meaning | Fix |
|---|---|---|
| Any test, with 401/403 from the admin API | Plain passwords are not accepted in `config.json` | Use the hashed password form `galenectl` produces, in both `GaleneContainer.ConfigJson` and `docker/galene/dev/config.json` |
| `PresenterToken_…` fails to join | The key wasn't installed | If `PUT …/.keys` returned 415, send `application/json`; if the API ignores it, put `"authKeys": [ … ]` in the group body instead |
| `ViewerToken_…` fails to join | Galène needs an explicit receive permission | Set `ViewerPermissions = new[] { "observe" }` |
| `TokenAudienceHost_…` | Galène checks the audience host | Keep `PublicUrl` for both clients and operator; operator connects through the public URL; set `canonicalHost` in production |
| `DuplicateUsername_…` | Galène replaces old connections itself | Delete the `HadLiveConnection` kick in Task 9 and this test |
| **GATE** `Unpresent_…`, `Kick_…`, `DeleteRoom_…` | Server-side moderation doesn't work as assumed | Investigate for at most one day (message names via `galene-protocol.md`). Still failing → **stop and re-plan on Janus** |

- [ ] **Step 12: Commit**

```bash
git add docker/galene AlSaqr.Infrastructure/Config/GaleneConfig.cs AlSaqr.Infrastructure/Video tests/AlSaqr.Tests
git commit -m "feat(video): add Galène control plane with gate tests"
```

---

### Task 4: Realtime broadcaster

**Files:**
- Create: `AlSaqr.Infrastructure/Video/SupabaseBroadcastClient.cs`
- Create: `AlSaqr.Infrastructure/Video/EventVideoBroadcaster.cs`
- Test: `tests/AlSaqr.Tests/Video/EventVideoBroadcasterTests.cs`

**Interfaces:**
- Consumes: `AlSaqr.Infrastructure.Config.SupabaseRealtimeConfig { Url; ServiceRoleSecret }` (existing, bound from `"Supabase"`).
- Produces: `ISupabaseBroadcastClient { Task BroadcastAsync(string topic, string eventName, object payload, CancellationToken ct = default); }`
- Produces: `IEventVideoBroadcaster` as spec §5, except `participant_joined` carries the full roster entry so clients render it without a refetch: `StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, ct)`, `StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, ct)`, `ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, ct)`, `ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, ct)`, `RoleChangedAsync(Guid eventId, Guid participantId, string role, ct)`.

- [ ] **Step 1: Write the failing tests**

`tests/AlSaqr.Tests/Video/EventVideoBroadcasterTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using AlSaqr.Tests.Support;
using Microsoft.Extensions.Options;

namespace AlSaqr.Tests.Video
{
    public class EventVideoBroadcasterTests
    {
        private const string ServiceRole = "service-role-secret-value";

        private readonly RecordingHandler _handler = new();
        private readonly EventVideoBroadcaster _broadcaster;

        public EventVideoBroadcasterTests()
        {
            var client = new SupabaseBroadcastClient(
                new HttpClient(_handler),
                Options.Create(new SupabaseRealtimeConfig { Url = "https://proj.supabase.test/", ServiceRoleSecret = ServiceRole }));
            _broadcaster = new EventVideoBroadcaster(client);
        }

        private JsonElement OnlyMessage()
        {
            var request = Assert.Single(_handler.Requests);
            using var doc = JsonDocument.Parse(request.Body);
            return doc.RootElement.GetProperty("messages")[0].Clone();
        }

        [Fact]
        public async Task StreamStarted_PostsToEventVideoTopic_WithServiceRoleHeaders()
        {
            var eventId = Guid.NewGuid();
            var streamId = Guid.NewGuid();

            await _broadcaster.StreamStartedAsync(eventId, streamId, DateTime.UtcNow);

            var request = Assert.Single(_handler.Requests);
            Assert.Equal("https://proj.supabase.test/realtime/v1/api/broadcast", request.Url);
            Assert.Equal(ServiceRole, request.Headers["apikey"]);
            Assert.Equal($"Bearer {ServiceRole}", request.Headers["Authorization"]);

            var message = OnlyMessage();
            Assert.Equal($"event_video:{eventId}", message.GetProperty("topic").GetString());
            Assert.Equal("stream_started", message.GetProperty("event").GetString());
            Assert.Equal(streamId, message.GetProperty("payload").GetProperty("videoStreamId").GetGuid());
        }

        [Fact]
        public async Task ParticipantJoined_CarriesFullRosterEntry()
        {
            var participant = new AlSaqr.Domain.Meetup.EventVideo.VideoParticipantDto
            {
                ParticipantId = Guid.NewGuid(),
                Username = "amina",
                Avatar = "https://cdn.test/a.png",
                Role = "viewer",
                Muted = true,
            };

            await _broadcaster.ParticipantJoinedAsync(Guid.NewGuid(), participant);

            var message = OnlyMessage();
            Assert.Equal("participant_joined", message.GetProperty("event").GetString());
            var payload = message.GetProperty("payload");
            Assert.Equal(participant.ParticipantId, payload.GetProperty("participantId").GetGuid());
            Assert.Equal("amina", payload.GetProperty("username").GetString());
            Assert.Equal("viewer", payload.GetProperty("role").GetString());
        }

        [Fact]
        public async Task ParticipantLeft_CarriesReason()
        {
            var participant = Guid.NewGuid();

            await _broadcaster.ParticipantLeftAsync(Guid.NewGuid(), participant, "reaped");

            var message = OnlyMessage();
            Assert.Equal("participant_left", message.GetProperty("event").GetString());
            Assert.Equal("reaped", message.GetProperty("payload").GetProperty("reason").GetString());
            Assert.Equal(participant, message.GetProperty("payload").GetProperty("participantId").GetGuid());
        }

        [Fact]
        public async Task FailedBroadcast_Throws_WithoutLeakingServiceRoleKey()
        {
            _handler.Status = HttpStatusCode.Unauthorized;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _broadcaster.RoleChangedAsync(Guid.NewGuid(), Guid.NewGuid(), "presenter"));

            Assert.Contains("role_changed", ex.Message);
            Assert.DoesNotContain(ServiceRole, ex.Message);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoBroadcasterTests"`
Expected: build FAIL — `The type or namespace name 'SupabaseBroadcastClient' could not be found`.

- [ ] **Step 3: Implement the broadcast client**

`AlSaqr.Infrastructure/Video/SupabaseBroadcastClient.cs`:

```csharp
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
            var url = $"{_config.Url.TrimEnd('/')}/realtime/v1/api/broadcast";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("apikey", _config.ServiceRoleSecret);
            request.Headers.Add("Authorization", $"Bearer {_config.ServiceRoleSecret}");
            request.Content = JsonContent.Create(new BroadcastRequest
            {
                Messages = new List<BroadcastMessage>
                {
                    new BroadcastMessage { Topic = topic, Event = eventName, Payload = payload },
                },
            });

            var response = await _httpClient.SendAsync(request, ct);
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
        }
    }
}
```

- [ ] **Step 4: Implement the broadcaster**

Create `AlSaqr.Infrastructure/Video/EventVideoBroadcaster.cs` with the **exact C# block from spec §5**, with one change: replace `ParticipantJoinedAsync` in the interface and the class with the roster-entry version (add `using static AlSaqr.Domain.Meetup.EventVideo;` at the top):

```csharp
        /// <summary>
        /// After a successful join — the "he's in the live" signal. Carries the full
        /// roster entry so other clients add it without refetching the roster.
        /// </summary>
        Task ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, CancellationToken ct = default);
```

```csharp
        public Task ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "participant_joined", participant, ct);
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoBroadcasterTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add AlSaqr.Infrastructure/Video/SupabaseBroadcastClient.cs AlSaqr.Infrastructure/Video/EventVideoBroadcaster.cs tests/AlSaqr.Tests/Video/EventVideoBroadcasterTests.cs
git commit -m "feat(video): add realtime broadcaster for event video"
```

---

### Task 5: Repository — authorization gates, start, live lookup

**Files:**
- Create: `AlSaqr.Data/Repositories/Meetup/Impl/IVideoStreamRepository.cs`
- Create: `AlSaqr.Data/Repositories/Meetup/VideoStreamRepository.cs`
- Create: `tests/AlSaqr.Tests/Video/VideoScenario.cs`
- Test: `tests/AlSaqr.Tests/Video/VideoStreamStartTests.cs`

**Interfaces:**
- Consumes: entities from Task 1, DTOs from Task 2, Plan A `Seed`/`PostgrestFixture`.
- Produces (repository, this task):
  - `Task<VideoStreamToDisplay?> GetLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct)`
  - `Task<VideoStreamToDisplay> RequireLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct)` — `NotFoundException` when none
  - `Task<VideoStream> StartEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)`
  - `Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct)` (needed here by the start-compensation test in Task 9; Task 7 adds `EndEventStream` on the same core)
  - `Task<List<VideoParticipant>> GetLiveParticipants(Supabase.Client supabase, Guid videoStreamId, CancellationToken ct)`
  - `Task MarkParticipantLeft(Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct)`
- Produces (tests): `VideoScenario.Create(Seed seed, bool isOnline = true) → Task<VideoScenario>` with `EventId, GroupId, FounderId, OrganizerId, AttendeeId, OutsiderId`, and `VideoScenario.AddAttendee(Seed seed, string username) → Task<Guid>`.

- [ ] **Step 1: Write the scenario builder**

`tests/AlSaqr.Tests/Video/VideoScenario.cs`:

```csharp
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// One online event in a group, with a founder (not an event attendee), an
    /// organizer, a plain attendee, and an outsider who attends nothing.
    /// </summary>
    public sealed record VideoScenario(
        Guid EventId, Guid GroupId, Guid FounderId, Guid OrganizerId, Guid AttendeeId, Guid OutsiderId)
    {
        public static async Task<VideoScenario> Create(Seed seed, bool isOnline = true)
        {
            var founder = await seed.User("founder");
            var organizer = await seed.User("organizer");
            var attendee = await seed.User("attendee");
            var outsider = await seed.User("outsider");

            var group = await seed.Group(founder);
            var eventId = await seed.Event(group, isOnline);

            await seed.EventAttendee(eventId, await seed.Attendee(organizer), group, isOrganizer: true);
            await seed.EventAttendee(eventId, await seed.Attendee(attendee), group);
            await seed.Attendee(outsider);   // has an attendee row, attends no event

            return new VideoScenario(eventId, group, founder, organizer, attendee, outsider);
        }

        public async Task<Guid> AddAttendee(Seed seed, string username)
        {
            var user = await seed.User(username);
            await seed.EventAttendee(EventId, await seed.Attendee(user), GroupId);
            return user;
        }
    }
}
```

- [ ] **Step 2: Write the failing tests (spec A1–A7, T4)**

`tests/AlSaqr.Tests/Video/VideoStreamStartTests.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Domain.Common;
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamStartTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private VideoScenario _s = default!;

        public VideoStreamStartTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _s = await VideoScenario.Create(_fx.Seed);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private Task<long> LiveStreamCount(Guid eventId) =>
            _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_streams where event_id = @e and ended_at is null", ("e", eventId));

        [Fact]
        public async Task Start_InPersonEvent_ThrowsValidation()
        {
            var inPerson = await _fx.Seed.Event(_s.GroupId, isOnline: false);

            await Assert.ThrowsAsync<ValidationException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, inPerson, default));
        }

        [Fact]
        public async Task Start_GrouplessEvent_ThrowsValidation()
        {
            var groupless = await _fx.Seed.Event(null, isOnline: true);

            await Assert.ThrowsAsync<ValidationException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, groupless, default));
        }

        [Fact]
        public async Task Start_UnknownEvent_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, Guid.NewGuid(), default));
        }

        [Theory]
        [InlineData("attendee")]
        [InlineData("outsider")]
        public async Task Start_ByNonOrganizer_ThrowsForbidden(string who)
        {
            var user = who == "attendee" ? _s.AttendeeId : _s.OutsiderId;

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.StartEventStream(_fx.Client, user, _s.EventId, default));
        }

        [Fact]
        public async Task Start_ByEventOrganizer_CreatesLiveStreamWithHostRow()
        {
            var stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.Equal(VideoStream.RoomNameFor(stream.Id), stream.RoomName);
            Assert.Equal(_s.OrganizerId, stream.HostId);
            var host = Assert.Single(await _repo.GetLiveParticipants(_fx.Client, stream.Id, default));
            Assert.Equal(VideoParticipant.RoleHost, host.Role);
            Assert.Equal(_s.OrganizerId, host.ParticipantId);
        }

        [Fact]
        public async Task Start_ByGroupFounder_Succeeds()
        {
            var stream = await _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            Assert.Equal(_s.FounderId, stream.HostId);
        }

        [Fact]
        public async Task Start_WhileLive_ThrowsConflict()
        {
            await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            await Assert.ThrowsAsync<ConflictException>(
                () => _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default));
        }

        [Fact]
        public async Task Start_Concurrently_OneWins_OtherGetsConflictNot500()
        {
            var results = await Task.WhenAll(
                Attempt(() => _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default)),
                Attempt(() => _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default)));

            Assert.Single(results, r => r == null);
            Assert.Single(results, r => r is ConflictException);
            Assert.Equal(1L, await LiveStreamCount(_s.EventId));
        }

        [Fact]
        public async Task NewStreamAfterEnd_HasDifferentRoomName()
        {
            var first = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.EndStreamSystem(_fx.Client, first, default);

            var second = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.NotEqual(first.RoomName, second.RoomName);
        }

        [Fact]
        public async Task EndStreamSystem_StampsEndedAt_AndMarksParticipantsEnded()
        {
            var stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            var ended = await _repo.EndStreamSystem(_fx.Client, stream, default);

            Assert.NotNull(ended.EndedAt);
            Assert.Equal(0L, await LiveStreamCount(_s.EventId));
            Assert.Equal("ended", await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where video_stream_id = @s", ("s", stream.Id)));
        }

        [Fact]
        public async Task GetLiveEventStream_ReturnsDisplay_OrNullWhenNone()
        {
            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));

            var stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            var display = await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default);

            Assert.Equal(stream.Id, display!.VideoStreamId);
            Assert.Equal("organizer", display.HostUsername);
            Assert.Equal("Test Event", display.EventName);
            Assert.Equal(1, display.ParticipantCount);
            Assert.True(display.IsLive);
        }

        [Fact]
        public async Task RequireLiveEventStream_WhenNone_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _repo.RequireLiveEventStream(_fx.Client, _s.EventId, default));
        }

        private static async Task<Exception?> Attempt(Func<Task> action)
        {
            try { await action(); return null; }
            catch (Exception ex) { return ex; }
        }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStreamStartTests"`
Expected: build FAIL — `The type or namespace name 'VideoStreamRepository' could not be found`.

- [ ] **Step 4: Write the interface (this task's members)**

`AlSaqr.Data/Repositories/Meetup/Impl/IVideoStreamRepository.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    /// <summary>
    /// Online-event video streams (specs/video-streaming-online-events.md §6). Every
    /// authorization rule of the feature lives here; methods are keyed on eventId
    /// because exactly one stream per event can be live.
    /// </summary>
    public interface IVideoStreamRepository
    {
        /// <summary>Live stream for an event, or null when none is live.</summary>
        Task<VideoStreamToDisplay?> GetLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct);

        /// <summary>Live stream for an event; NotFound when none is live.</summary>
        Task<VideoStreamToDisplay> RequireLiveEventStream(Supabase.Client supabase, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Starts the stream with the caller as host. Validation when the event is not
        /// online (C1) or has no host group; Forbidden unless the caller is the event
        /// organizer or group founder; Conflict when a stream is already live.
        /// </summary>
        Task<VideoStream> StartEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>System end (reaper, failed start): no authority check.</summary>
        Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct);

        /// <summary>Participants with left_at null, ordered joined_at asc, id asc.</summary>
        Task<List<VideoParticipant>> GetLiveParticipants(Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

        /// <summary>Stamps left_at + left_reason and clears the SFU identity.</summary>
        Task MarkParticipantLeft(Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct);
    }
}
```

- [ ] **Step 5: Implement**

`AlSaqr.Data/Repositories/Meetup/VideoStreamRepository.cs`:

```csharp
using System.Globalization;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Common;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using static AlSaqr.Domain.Meetup.EventVideo;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Meetup
{
    /// <summary>
    /// Video streams for online events (specs/video-streaming-online-events.md §6).
    /// The backend is authoritative for attendance and roles: every gate is checked
    /// here, before the controller makes any SFU call or mints any token.
    /// </summary>
    public class VideoStreamRepository : IVideoStreamRepository
    {
        public async Task<VideoStreamToDisplay?> GetLiveEventStream(
            Supabase.Client supabase, Guid eventId, CancellationToken ct)
        {
            var stream = await FindLiveStream(supabase, eventId, ct);
            return stream == null ? null : await ToDisplay(supabase, stream, ct);
        }

        public async Task<VideoStreamToDisplay> RequireLiveEventStream(
            Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            await ToDisplay(supabase, await GetLiveStreamOrThrow(supabase, eventId, ct), ct);

        /// <summary>
        /// The pre-check is an optimisation; the partial unique index is the guard. Two
        /// concurrent starts both pass the pre-check and PostgREST returns 23505 for the
        /// loser, which must surface as 409, not 500.
        /// </summary>
        public async Task<VideoStream> StartEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);

            if (!await CanControlEventVideo(supabase, hostEvent, userId, ct))
                throw new ForbiddenException("Only the event organizer or group founder may start the video stream.");

            if (await FindLiveStream(supabase, eventId, ct) != null)
                throw new ConflictException("This event already has a live video stream.");

            var streamId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            try
            {
                var created = (await supabase
                    .From<VideoStream>()
                    .Insert(
                        new VideoStream
                        {
                            Id = streamId,
                            EventId = eventId,
                            RoomName = VideoStream.RoomNameFor(streamId),   // stream-scoped (§4)
                            HostId = userId,
                            StartedAt = now,
                        },
                        new QueryOptions { Returning = ReturnType.Representation },
                        ct))
                    .Models.FirstOrDefault()
                    ?? throw new ConflictException("The video stream could not be created.");

                await supabase
                    .From<VideoParticipant>()
                    .Insert(
                        new VideoParticipant
                        {
                            Id = Guid.NewGuid(),
                            EventId = eventId,
                            ParticipantId = userId,
                            VideoStreamId = created.Id,
                            Role = VideoParticipant.RoleHost,
                            // No SFU identity until the host actually joins: it marks "has a
                            // token and may be connected", which drives second-device kicks.
                            JoinedAt = now,
                            LastSeenAt = now,
                        },
                        new QueryOptions { Returning = ReturnType.Minimal },
                        ct);

                return created;
            }
            catch (PostgrestException ex) when (IsUniqueViolation(ex))
            {
                // Lost the race against a concurrent start.
                throw new ConflictException("This event already has a live video stream.");
            }
        }

        public Task<VideoStream> EndStreamSystem(Supabase.Client supabase, VideoStream stream, CancellationToken ct) =>
            EndStream(supabase, stream, VideoParticipant.LeftEnded, ct);

        public async Task<List<VideoParticipant>> GetLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Order("joined_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task MarkParticipantLeft(
            Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct)
        {
            participant.LeftAt = DateTime.UtcNow;
            participant.LeftReason = reason;
            participant.SfuIdentity = null;

            await supabase
                .From<VideoParticipant>()
                .Upsert(participant, new QueryOptions { Returning = ReturnType.Minimal }, ct);
        }

        // ----- stream lifecycle -----

        private async Task<VideoStream> EndStream(
            Supabase.Client supabase, VideoStream stream, string reason, CancellationToken ct)
        {
            foreach (var participant in await GetLiveParticipants(supabase, stream.Id, ct))
                await MarkParticipantLeft(supabase, participant, reason, ct);

            stream.EndedAt = DateTime.UtcNow;

            return (await supabase
                    .From<VideoStream>()
                    .Upsert(stream, new QueryOptions { Returning = ReturnType.Representation }, ct))
                .Models.First();
        }

        private static Task<VideoStream?> FindLiveStream(Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            supabase
                .From<VideoStream>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Filter("ended_at", Operator.Is, "null")
                .Single(ct);

        private static async Task<VideoStream> GetLiveStreamOrThrow(
            Supabase.Client supabase, Guid eventId, CancellationToken ct) =>
            await FindLiveStream(supabase, eventId, ct)
            ?? throw new NotFoundException("No live video stream for this event.");

        private static async Task<VideoStreamToDisplay> ToDisplay(
            Supabase.Client supabase, VideoStream stream, CancellationToken ct)
        {
            var hostEvent = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, stream.EventId.ToString())
                .Single(ct);

            var host = await supabase
                .From<AlSaqrUser>()
                .Filter("id", Operator.Equals, stream.HostId.ToString())
                .Single(ct);

            var participantCount = await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Count(CountType.Exact, ct);

            return new VideoStreamToDisplay
            {
                VideoStreamId = stream.Id,
                EventId = stream.EventId,
                EventName = hostEvent?.Name,
                HostId = stream.HostId,
                HostUsername = host?.Username,
                HostAvatar = host?.Avatar,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                ParticipantCount = participantCount,
                IsLive = stream.EndedAt == null,
            };
        }

        // ----- authorization gates (spec §6.1) -----

        // C1: online events only, checked before any SFU call. A groupless event has
        // no attendees (event_attendees.group_id is NOT NULL), so no one to authorize.
        private static async Task<Event> GetOnlineEventOrThrow(
            Supabase.Client supabase, Guid eventId, CancellationToken ct)
        {
            var existing = await supabase
                .From<Event>()
                .Filter("id", Operator.Equals, eventId.ToString())
                .Single(ct)
                ?? throw new NotFoundException($"Event {eventId} does not exist.");

            if (!existing.IsOnline)
                throw new ValidationException("Video streaming is available for online events only.");

            if (existing.GroupId is null)
                throw new ValidationException("This event has no host group and cannot host video.");

            return existing;
        }

        // Attendance lives on event_attendees, keyed on attendees.id.
        private static async Task<bool> IsEventAttendee(
            Supabase.Client supabase, Guid eventId, Guid userId, CancellationToken ct)
        {
            var attendee = await supabase
                .From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (attendee == null)
                return false;

            var attending = await supabase
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Single(ct);

            return attending != null;
        }

        // Two legitimate authorities: the host group's founder (checked first, the
        // escalation path) and the event organizer.
        private static async Task<bool> CanControlEventVideo(
            Supabase.Client supabase, Event hostEvent, Guid userId, CancellationToken ct)
        {
            var founded = await supabase
                .From<Groups>()
                .Filter("id", Operator.Equals, hostEvent.GroupId!.Value.ToString())
                .Filter("founder_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (founded != null)
                return true;

            var attendee = await supabase
                .From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Single(ct);

            if (attendee == null)
                return false;

            var organizer = await supabase
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, hostEvent.Id.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Filter("is_event_organizer", Operator.Equals, "true")
                .Single(ct);

            return organizer != null;
        }

        /// <summary>PostgreSQL 23505 — unique_violation. Content is PostgREST's JSON error body.</summary>
        private static bool IsUniqueViolation(PostgrestException ex) =>
            ex.Content?.Contains("23505", StringComparison.Ordinal) == true;

        /// <summary>UTC ISO-8601 with microseconds, for PostgREST timestamp filters.</summary>
        private static string ToUtcIso(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    }
}
```

`IsEventAttendee` and `ToUtcIso` are first used in Task 6. If the build treats unused private members as errors, add the first caller in Task 6 rather than deleting them.

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStreamStartTests"`
Expected: PASS (13 tests).

If `Start_Concurrently_...` returns a non-Conflict exception for the loser, print `ex.GetType()` and `((PostgrestException)ex).Content`. The body must contain `"code":"23505"`; if it differs, match what PostgREST actually sends.

- [ ] **Step 7: Commit**

```bash
git add AlSaqr.Data/Repositories/Meetup tests/AlSaqr.Tests/Video
git commit -m "feat(video): add stream start and authorization gates"
```

---

### Task 6: Repository — join, leave, heartbeat, camera, roster

**Files:**
- Modify: `AlSaqr.Data/Repositories/Meetup/Impl/IVideoStreamRepository.cs`
- Modify: `AlSaqr.Data/Repositories/Meetup/VideoStreamRepository.cs`
- Test: `tests/AlSaqr.Tests/Video/VideoStreamParticipationTests.cs`

**Interfaces:**
- Produces (added to the interface):
  - `Task<(VideoStream Stream, VideoParticipant Participant, bool HadLiveConnection)> JoinEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)` — `Participant.SfuIdentity` is always set; `HadLiveConnection` is true when the caller was already live with an SFU identity (second device or reload), so the controller must kick the older connection
  - `Task<(VideoStream Stream, VideoParticipant Snapshot)?> LeaveEventStream(Supabase.Client supabase, Guid userId, Guid eventId, string reason, CancellationToken ct)` — `null` when not live (idempotent); `Snapshot` is pre-leave, so it still has `SfuIdentity`
  - `Task<(VideoStream Stream, VideoParticipant Participant)> TouchLiveParticipant(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)` — refreshes `last_seen_at`; NotFound if no live stream, Forbidden if caller not live. Backs both `heartbeat` and `token` refresh (Task 9)
  - `Task SetCameraState(Supabase.Client supabase, Guid userId, Guid eventId, bool cameraEnabled, bool muted, CancellationToken ct)`
  - `Task<List<VideoParticipantDto>> GetParticipants(Supabase.Client supabase, Guid videoStreamId, CancellationToken ct)`

- [ ] **Step 1: Write the failing tests (A8–A10 repo level, L8, L9, L10′, L11, C1)**

`tests/AlSaqr.Tests/Video/VideoStreamParticipationTests.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Domain.Common;
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamParticipationTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private VideoScenario _s = default!;
        private VideoStream _stream = default!;

        public VideoStreamParticipationTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _s = await VideoScenario.Create(_fx.Seed);
            _stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private Task SetLeft(Guid userId, string reason, string ago) =>
            _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '{ago}', left_reason = @r, sfu_identity = null " +
                "where video_stream_id = @s and participant_id = @u",
                ("r", reason), ("s", _stream.Id), ("u", userId));

        private Task SetRole(Guid userId, string role) =>
            _fx.Seed.Execute(
                $"update {S}.video_participants set role = @r where video_stream_id = @s and participant_id = @u",
                ("r", role), ("s", _stream.Id), ("u", userId));

        [Fact]
        public async Task Join_NonAttendee_ThrowsForbidden()
        {
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.JoinEventStream(_fx.Client, _s.OutsiderId, _s.EventId, default));
        }

        [Fact]
        public async Task Join_Attendee_EntersAsViewer_WithStreamScopedIdentity()
        {
            var (stream, participant, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(_stream.Id, stream.Id);
            Assert.Equal(VideoParticipant.RoleViewer, participant.Role);
            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", participant.SfuIdentity);
            Assert.Null(participant.LeftAt);
        }

        [Fact]
        public async Task Join_WhenNoLiveStream_ThrowsNotFound()
        {
            await _repo.EndStreamSystem(_fx.Client, _stream, default);

            await Assert.ThrowsAsync<NotFoundException>(
                () => _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task Join_FounderWhoIsNotAnAttendee_JoinsAsHost()
        {
            await _repo.EndStreamSystem(_fx.Client, _stream, default);
            await _repo.StartEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            var (_, participant, _) = await _repo.JoinEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleHost, participant.Role);
        }

        [Fact]
        public async Task DuplicateJoin_WhileLive_PreservesPresenterRole()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetRole(_s.AttendeeId, VideoParticipant.RolePresenter);

            var (_, again, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RolePresenter, again.Role);
        }

        [Fact]
        public async Task SecondDevice_DoesNotCreateSecondRow_AndReportsLiveConnection()
        {
            var (_, _, firstHadConnection) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            var (_, _, secondHadConnection) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.False(firstHadConnection);
            Assert.True(secondHadConnection);   // Galène allows duplicate usernames: the controller kicks the older one
            Assert.Equal(1L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where video_stream_id = @s and participant_id = @u",
                ("s", _stream.Id), ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Rejoin_WithinGrace_KeepsPresenterRole()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetRole(_s.AttendeeId, VideoParticipant.RolePresenter);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "4 minutes");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RolePresenter, rejoined.Role);
            Assert.Null(rejoined.LeftAt);
            Assert.Null(rejoined.LeftReason);
        }

        [Fact]
        public async Task Rejoin_AfterGrace_DowngradesToViewer()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetRole(_s.AttendeeId, VideoParticipant.RolePresenter);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "6 minutes");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleViewer, rejoined.Role);
        }

        [Fact]
        public async Task Rejoin_AfterDemotionAndDrop_StaysViewer()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            // A demote persists role = viewer; then the client drops.
            await SetRole(_s.AttendeeId, VideoParticipant.RoleViewer);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "1 minute");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleViewer, rejoined.Role);
        }

        [Fact]
        public async Task HostLeavesAndRejoinsAfterGrace_ResumesHost()
        {
            await SetLeft(_s.OrganizerId, VideoParticipant.LeftExplicit, "10 minutes");

            var (_, rejoined, _) = await _repo.JoinEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.Equal(VideoParticipant.RoleHost, rejoined.Role);
        }

        [Fact]
        public async Task Leave_ReturnsPreLeaveSnapshot_AndMarksExplicit()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            var left = await _repo.LeaveEventStream(
                _fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default);

            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", left!.Value.Snapshot.SfuIdentity);
            Assert.Equal(_stream.RoomName, left.Value.Stream.RoomName);
            Assert.Equal("explicit", await _fx.Seed.Scalar<string>(
                $"select left_reason from {S}.video_participants where video_stream_id = @s and participant_id = @u",
                ("s", _stream.Id), ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Leave_Twice_SecondReturnsNull()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await _repo.LeaveEventStream(_fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default);

            Assert.Null(await _repo.LeaveEventStream(
                _fx.Client, _s.AttendeeId, _s.EventId, VideoParticipant.LeftExplicit, default));
        }

        [Fact]
        public async Task Heartbeat_RefreshesLastSeen()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = now() - interval '50 seconds' where participant_id = @u",
                ("u", _s.AttendeeId));

            await _repo.TouchLiveParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default);

            Assert.True(await _fx.Seed.Scalar<bool>(
                $"select last_seen_at > now() - interval '10 seconds' from {S}.video_participants where participant_id = @u",
                ("u", _s.AttendeeId)));
        }

        [Fact]
        public async Task Heartbeat_AfterReaped_ThrowsForbidden()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
            await SetLeft(_s.AttendeeId, VideoParticipant.LeftReaped, "5 seconds");

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.TouchLiveParticipant(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task SetCameraState_Persists()
        {
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);

            await _repo.SetCameraState(_fx.Client, _s.AttendeeId, _s.EventId, cameraEnabled: true, muted: false, default);

            var roster = await _repo.GetParticipants(_fx.Client, _stream.Id, default);
            var me = Assert.Single(roster, p => p.ParticipantId == _s.AttendeeId);
            Assert.True(me.CameraEnabled);
            Assert.False(me.Muted);
        }

        [Fact]
        public async Task Roster_IsDeterministicallyOrdered_AndExcludesLeft()
        {
            var extra1 = await _s.AddAttendee(_fx.Seed, "extra1");
            var extra2 = await _s.AddAttendee(_fx.Seed, "extra2");
            foreach (var user in new[] { _s.AttendeeId, extra1, extra2 })
                await _repo.JoinEventStream(_fx.Client, user, _s.EventId, default);
            await _repo.LeaveEventStream(_fx.Client, extra2, _s.EventId, VideoParticipant.LeftExplicit, default);

            // Force a joined_at tie so only the id tie-breaker decides.
            await _fx.Seed.Execute(
                $"update {S}.video_participants set joined_at = '2026-01-01T00:00:00Z' where video_stream_id = @s",
                ("s", _stream.Id));

            var expected = new List<Guid>();
            await using (var conn = new Npgsql.NpgsqlConnection(_fx.ConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = new Npgsql.NpgsqlCommand(
                    $"select participant_id from {S}.video_participants where video_stream_id = @s and left_at is null " +
                    "order by joined_at asc, id asc", conn);
                cmd.Parameters.AddWithValue("s", _stream.Id);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    expected.Add(reader.GetGuid(0));
            }

            var first = (await _repo.GetParticipants(_fx.Client, _stream.Id, default)).Select(p => p.ParticipantId).ToList();
            var second = (await _repo.GetParticipants(_fx.Client, _stream.Id, default)).Select(p => p.ParticipantId).ToList();

            Assert.Equal(expected, first);
            Assert.Equal(first, second);
            Assert.DoesNotContain(extra2, first);
            Assert.Equal("organizer", (await _repo.GetParticipants(_fx.Client, _stream.Id, default))
                .Single(p => p.ParticipantId == _s.OrganizerId).Username);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStreamParticipationTests"`
Expected: build FAIL — `'VideoStreamRepository' does not contain a definition for 'JoinEventStream'`.

- [ ] **Step 3: Add the interface members**

Append inside `IVideoStreamRepository`:

```csharp
        /// <summary>
        /// Registers the caller on the live stream. NotFound when no stream is live;
        /// Forbidden unless the caller attends the event or may control its video.
        /// Re-checked on every join. Honours the 5-minute rejoin grace.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant, bool HadLiveConnection)> JoinEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>
        /// Marks the caller left with the reason. Returns the stream and the pre-leave
        /// snapshot (for the SFU disconnect), or null when the caller was not live.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Snapshot)?> LeaveEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, string reason, CancellationToken ct);

        /// <summary>
        /// Refreshes last_seen_at and returns the caller's live row with its stream.
        /// NotFound for no live stream, Forbidden when not live on it.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant)> TouchLiveParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        /// <summary>Persists camera/mute state (a cost signal, not a permission).</summary>
        Task SetCameraState(
            Supabase.Client supabase, Guid userId, Guid eventId, bool cameraEnabled, bool muted, CancellationToken ct);

        /// <summary>Live roster, ordered joined_at asc, id asc.</summary>
        Task<List<VideoParticipantDto>> GetParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);
```

- [ ] **Step 4: Implement**

Add to `VideoStreamRepository` (public members after `RequireLiveEventStream`; private helpers in the "stream lifecycle" region):

```csharp
        private static readonly TimeSpan RejoinGrace = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Role on (re)join: the host always resumes 'host'; a participant who left
        /// within the grace window resumes their persisted role; anyone else is
        /// 'viewer'. A join while already live (second tab, double click, reload) leaves
        /// the role untouched and reports HadLiveConnection, so the controller kicks the
        /// older SFU connection: one seat per user. (Galène, unlike LiveKit, lets two
        /// connections share a username.)
        /// </summary>
        public async Task<(VideoStream Stream, VideoParticipant Participant, bool HadLiveConnection)> JoinEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

            // Re-checked on EVERY join. Controllers count too: a founder can start a
            // stream without being an event attendee (correction 7).
            if (!await IsEventAttendee(supabase, eventId, userId, ct)
                && !await CanControlEventVideo(supabase, hostEvent, userId, ct))
                throw new ForbiddenException("Only users attending this event may join its video stream.");

            var existing = await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
                .Filter("participant_id", Operator.Equals, userId.ToString())
                .Single(ct);

            var now = DateTime.UtcNow;
            var hadLiveConnection = existing is { LeftAt: null, SfuIdentity: not null };

            if (existing == null)
            {
                existing = new VideoParticipant
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    ParticipantId = userId,
                    VideoStreamId = stream.Id,
                    Role = stream.HostId == userId ? VideoParticipant.RoleHost : VideoParticipant.RoleViewer,
                    JoinedAt = now,
                };
            }
            else if (existing.LeftAt != null)
            {
                var keepsRole = stream.HostId == userId || await LeftWithinGrace(supabase, existing.Id, now, ct);
                if (!keepsRole)
                    existing.Role = VideoParticipant.RoleViewer;

                existing.LeftAt = null;
                existing.LeftReason = null;
            }

            existing.SfuIdentity = VideoParticipant.SfuIdentityFor(stream.Id, userId);
            existing.LastSeenAt = now;

            await supabase
                .From<VideoParticipant>()
                .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return (stream, existing, hadLiveConnection);
        }

        public async Task<(VideoStream Stream, VideoParticipant Snapshot)?> LeaveEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, string reason, CancellationToken ct)
        {
            var stream = await FindLiveStream(supabase, eventId, ct);
            if (stream == null)
                return null;

            var live = await FindLiveRow(supabase, stream.Id, userId, ct);
            if (live == null)
                return null;

            var snapshot = Copy(live);
            await MarkParticipantLeft(supabase, live, reason, ct);
            return (stream, snapshot);
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)> TouchLiveParticipant(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var (stream, live) = await GetLiveRowOrThrow(supabase, userId, eventId, ct);
            live.LastSeenAt = DateTime.UtcNow;

            await supabase
                .From<VideoParticipant>()
                .Upsert(live, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return (stream, live);
        }

        public async Task SetCameraState(
            Supabase.Client supabase, Guid userId, Guid eventId, bool cameraEnabled, bool muted, CancellationToken ct)
        {
            var (_, live) = await GetLiveRowOrThrow(supabase, userId, eventId, ct);
            live.CameraEnabled = cameraEnabled;
            live.Muted = muted;
            live.LastSeenAt = DateTime.UtcNow;

            await supabase
                .From<VideoParticipant>()
                .Upsert(live, new QueryOptions { Returning = ReturnType.Minimal }, ct);
        }

        public async Task<List<VideoParticipantDto>> GetParticipants(
            Supabase.Client supabase, Guid videoStreamId, CancellationToken ct)
        {
            var rows = await GetLiveParticipants(supabase, videoStreamId, ct);
            if (rows.Count == 0)
                return new List<VideoParticipantDto>();

            var users = (await supabase
                    .From<AlSaqrUser>()
                    .Filter("id", Operator.In, rows.Select(r => r.ParticipantId.ToString()).ToList())
                    .Get(ct))
                .Models
                .ToDictionary(u => u.Id);

            return rows
                .Select(r =>
                {
                    users.TryGetValue(r.ParticipantId, out var user);
                    return new VideoParticipantDto
                    {
                        ParticipantId = r.ParticipantId,
                        Username = user?.Username ?? string.Empty,
                        Avatar = user?.Avatar,
                        Role = r.Role,
                        CameraEnabled = r.CameraEnabled,
                        Muted = r.Muted,
                    };
                })
                .ToList();
        }
```

Private helpers (add to the "stream lifecycle" region):

```csharp
        private static Task<VideoParticipant?> FindLiveRow(
            Supabase.Client supabase, Guid videoStreamId, Guid userId, CancellationToken ct) =>
            supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("participant_id", Operator.Equals, userId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Single(ct);

        // The 403 detail is part of the client contract: "not live → call join again".
        private static async Task<(VideoStream Stream, VideoParticipant Participant)> GetLiveRowOrThrow(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);
            var live = await FindLiveRow(supabase, stream.Id, userId, ct)
                ?? throw new ForbiddenException("You are not live on this video stream. Join again.");
            return (stream, live);
        }

        // Evaluated in SQL, never by comparing a deserialized DateTime in C#.
        private static async Task<bool> LeftWithinGrace(
            Supabase.Client supabase, Guid participantRowId, DateTime utcNow, CancellationToken ct) =>
            await supabase
                .From<VideoParticipant>()
                .Filter("id", Operator.Equals, participantRowId.ToString())
                .Filter("left_at", Operator.GreaterThanOrEqual, ToUtcIso(utcNow - RejoinGrace))
                .Single(ct) != null;

        private static VideoParticipant Copy(VideoParticipant p) => new()
        {
            Id = p.Id,
            EventId = p.EventId,
            ParticipantId = p.ParticipantId,
            VideoStreamId = p.VideoStreamId,
            Role = p.Role,
            CameraEnabled = p.CameraEnabled,
            Muted = p.Muted,
            SfuIdentity = p.SfuIdentity,
            JoinedAt = p.JoinedAt,
            LeftAt = p.LeftAt,
            LeftReason = p.LeftReason,
            LastSeenAt = p.LastSeenAt,
        };
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStream"`
Expected: PASS (13 start + 16 participation = 29 tests).

- [ ] **Step 6: Commit**

```bash
git add AlSaqr.Data/Repositories/Meetup tests/AlSaqr.Tests/Video/VideoStreamParticipationTests.cs
git commit -m "feat(video): add join/leave/heartbeat/roster"
```

---

### Task 7: Repository — presenters, end, reaper queries, retention

**Files:**
- Modify: `AlSaqr.Data/Repositories/Meetup/Impl/IVideoStreamRepository.cs`
- Modify: `AlSaqr.Data/Repositories/Meetup/VideoStreamRepository.cs`
- Test: `tests/AlSaqr.Tests/Video/VideoStreamControlTests.cs`

**Interfaces:**
- Produces (added to the interface):
  - `Task<(VideoStream Stream, VideoParticipant Participant)> PromotePresenter(Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, int maxPresenters, CancellationToken ct)`
  - `Task<(VideoStream Stream, VideoParticipant Participant)> DemotePresenter(Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct)`
  - `Task<VideoStream> EndEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)`
  - `Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct)`
  - `Task<List<VideoStream>> GetLiveStreamsStartedBefore(Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct)`
  - `Task<List<VideoParticipant>> GetStaleLiveParticipants(Supabase.Client supabase, Guid videoStreamId, DateTime cutoffUtc, CancellationToken ct)`
  - `Task<int> PurgeParticipantsLeftBefore(Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct)`

- [ ] **Step 1: Write the failing tests (A11–A13, L12, demote)**

`tests/AlSaqr.Tests/Video/VideoStreamControlTests.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Domain.Common;
using AlSaqr.Tests.Support;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamControlTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private VideoScenario _s = default!;
        private VideoStream _stream = default!;

        public VideoStreamControlTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _s = await VideoScenario.Create(_fx.Seed);
            _stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Promote_ByHost_MakesViewerPresenter()
        {
            var (stream, promoted) = await _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            Assert.Equal(_stream.RoomName, stream.RoomName);
            Assert.Equal(VideoParticipant.RolePresenter, promoted.Role);
            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", promoted.SfuIdentity);
        }

        [Fact]
        public async Task Promote_ByFounder_IsAllowed()
        {
            var (_, promoted) = await _repo.PromotePresenter(
                _fx.Client, _s.FounderId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            Assert.Equal(VideoParticipant.RolePresenter, promoted.Role);
        }

        [Fact]
        public async Task Promote_ByPlainAttendee_ThrowsForbidden()
        {
            var other = await _s.AddAttendee(_fx.Seed, "other");
            await _repo.JoinEventStream(_fx.Client, other, _s.EventId, default);

            await Assert.ThrowsAsync<ForbiddenException>(() => _repo.PromotePresenter(
                _fx.Client, _s.AttendeeId, _s.EventId, other, maxPresenters: 9, default));
        }

        [Fact]
        public async Task Promote_TargetNotLive_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.OutsiderId, maxPresenters: 9, default));
        }

        [Fact]
        public async Task Promote_BeyondPresenterCap_ThrowsConflict()
        {
            // Cap of 2: the host is one publisher, so exactly one promotion fits.
            var second = await _s.AddAttendee(_fx.Seed, "second");
            await _repo.JoinEventStream(_fx.Client, second, _s.EventId, default);
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 2, default);

            await Assert.ThrowsAsync<ConflictException>(() => _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, second, maxPresenters: 2, default));
        }

        [Fact]
        public async Task Promote_AlreadyPresenter_IsIdempotentAndIgnoresCap()
        {
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 2, default);

            var (_, again) = await _repo.PromotePresenter(
                _fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 2, default);

            Assert.Equal(VideoParticipant.RolePresenter, again.Role);
        }

        [Fact]
        public async Task Demote_Presenter_BecomesViewer()
        {
            await _repo.PromotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, maxPresenters: 9, default);

            var (_, demoted) = await _repo.DemotePresenter(_fx.Client, _s.OrganizerId, _s.EventId, _s.AttendeeId, default);

            Assert.Equal(VideoParticipant.RoleViewer, demoted.Role);
            Assert.Equal($"{_stream.Id}:{_s.AttendeeId}", demoted.SfuIdentity);
        }

        [Fact]
        public async Task Demote_Host_ThrowsValidation()
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => _repo.DemotePresenter(_fx.Client, _s.FounderId, _s.EventId, _s.OrganizerId, default));
        }

        [Fact]
        public async Task End_ByPlainAttendee_ThrowsForbidden()
        {
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _repo.EndEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default));
        }

        [Fact]
        public async Task End_ByHost_EndsStream_AndMarksEveryoneEnded()
        {
            var ended = await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);

            Assert.NotNull(ended.EndedAt);
            Assert.Equal(_stream.RoomName, ended.RoomName);
            Assert.Equal(0L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where video_stream_id = @s and left_at is null",
                ("s", _stream.Id)));
            Assert.Equal(2L, await _fx.Seed.Scalar<long>(
                $"select count(*) from {S}.video_participants where video_stream_id = @s and left_reason = 'ended'",
                ("s", _stream.Id)));
        }

        [Fact]
        public async Task End_ByFounder_EndsAStreamSomeoneElseStarted()
        {
            var ended = await _repo.EndEventStream(_fx.Client, _s.FounderId, _s.EventId, default);

            Assert.NotNull(ended.EndedAt);
        }

        [Fact]
        public async Task GetStaleLiveParticipants_UsesCutoffInSql()
        {
            await _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = now() - interval '61 seconds' where participant_id = @u",
                ("u", _s.AttendeeId));

            var stale = await _repo.GetStaleLiveParticipants(
                _fx.Client, _stream.Id, DateTime.UtcNow.AddSeconds(-60), default);

            Assert.Equal(_s.AttendeeId, Assert.Single(stale).ParticipantId);
        }

        [Fact]
        public async Task GetLiveStreamsStartedBefore_FindsOnlyOldStreams()
        {
            Assert.Empty(await _repo.GetLiveStreamsStartedBefore(_fx.Client, DateTime.UtcNow.AddHours(-4), default));

            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = now() - interval '4 hours 1 minute' where id = @s",
                ("s", _stream.Id));

            Assert.Single(await _repo.GetLiveStreamsStartedBefore(_fx.Client, DateTime.UtcNow.AddHours(-4), default));
        }

        [Fact]
        public async Task Purge_DeletesParticipantsOlderThanCutoff_KeepsNewer()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '91 days' where participant_id = @old",
                ("old", _s.AttendeeId));
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '89 days' where participant_id = @kept",
                ("kept", _s.OrganizerId));

            var purged = await _repo.PurgeParticipantsLeftBefore(_fx.Client, DateTime.UtcNow.AddDays(-90), default);

            Assert.Equal(1, purged);
            Assert.Equal(1L, await _fx.Seed.Scalar<long>($"select count(*) from {S}.video_participants"));
            Assert.Equal(_s.OrganizerId, await _fx.Seed.Scalar<Guid>($"select participant_id from {S}.video_participants"));
        }

        [Fact]
        public async Task Purge_DeletesStreamsEndedBeforeCutoff()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '91 days'");
            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = now() - interval '92 days', ended_at = now() - interval '91 days'");

            await _repo.PurgeParticipantsLeftBefore(_fx.Client, DateTime.UtcNow.AddDays(-90), default);

            Assert.Equal(0L, await _fx.Seed.Scalar<long>($"select count(*) from {S}.video_streams"));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStreamControlTests"`
Expected: build FAIL — `'VideoStreamRepository' does not contain a definition for 'PromotePresenter'`.

- [ ] **Step 3: Add the interface members**

Append inside `IVideoStreamRepository`:

```csharp
        /// <summary>
        /// Viewer → presenter. Forbidden unless the caller is the host, event organizer,
        /// or group founder; NotFound when the target is not live; Conflict when
        /// host + presenters already equals maxPresenters (C5). Idempotent for an
        /// existing presenter or host.
        /// </summary>
        Task<(VideoStream Stream, VideoParticipant Participant)> PromotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, int maxPresenters, CancellationToken ct);

        /// <summary>Presenter → viewer. Same authority as promote; Validation for the host.</summary>
        Task<(VideoStream Stream, VideoParticipant Participant)> DemotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct);

        /// <summary>
        /// Ends the live stream (host, event organizer, or group founder; Forbidden
        /// otherwise). Marks every participant left with reason 'ended'.
        /// </summary>
        Task<VideoStream> EndEventStream(Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

        // ----- Reaper support (spec §7) -----

        Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct);

        /// <summary>Live streams whose started_at is before the cutoff (C11 expiry).</summary>
        Task<List<VideoStream>> GetLiveStreamsStartedBefore(
            Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct);

        /// <summary>Live participants whose last_seen_at is before the cutoff (C9).</summary>
        Task<List<VideoParticipant>> GetStaleLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, DateTime cutoffUtc, CancellationToken ct);

        // ----- Retention (spec §9) -----

        /// <summary>
        /// Deletes participant rows with left_at before the cutoff, then streams that
        /// ended before it. Returns the number of participant rows deleted.
        /// </summary>
        Task<int> PurgeParticipantsLeftBefore(Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct);
```

- [ ] **Step 4: Implement**

Add to `VideoStreamRepository`:

```csharp
        public async Task<(VideoStream Stream, VideoParticipant Participant)> PromotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, int maxPresenters, CancellationToken ct)
        {
            var (stream, target) = await GetControlledTarget(supabase, callerId, eventId, targetUserId, ct);

            // Host and presenters already publish.
            if (target.Role != VideoParticipant.RoleViewer)
                return (stream, target);

            var publishers = await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Filter("role", Operator.In, new List<string> { VideoParticipant.RoleHost, VideoParticipant.RolePresenter })
                .Count(CountType.Exact, ct);

            if (publishers >= maxPresenters)
                throw new ConflictException($"This stream already has the maximum of {maxPresenters} presenters.");

            target.Role = VideoParticipant.RolePresenter;
            await supabase
                .From<VideoParticipant>()
                .Upsert(target, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return (stream, target);
        }

        public async Task<(VideoStream Stream, VideoParticipant Participant)> DemotePresenter(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct)
        {
            var (stream, target) = await GetControlledTarget(supabase, callerId, eventId, targetUserId, ct);

            if (target.Role == VideoParticipant.RoleHost)
                throw new ValidationException("The host cannot be demoted.");

            if (target.Role == VideoParticipant.RolePresenter)
            {
                target.Role = VideoParticipant.RoleViewer;
                await supabase
                    .From<VideoParticipant>()
                    .Upsert(target, new QueryOptions { Returning = ReturnType.Minimal }, ct);
            }

            return (stream, target);
        }

        public async Task<VideoStream> EndEventStream(
            Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

            if (stream.HostId != userId && !await CanControlEventVideo(supabase, hostEvent, userId, ct))
                throw new ForbiddenException(
                    "Only the stream host, event organizer, or group founder may end the video stream.");

            return await EndStream(supabase, stream, VideoParticipant.LeftEnded, ct);
        }

        public async Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct) =>
            (await supabase
                .From<VideoStream>()
                .Filter("ended_at", Operator.Is, "null")
                .Order("started_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task<List<VideoStream>> GetLiveStreamsStartedBefore(
            Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct) =>
            (await supabase
                .From<VideoStream>()
                .Filter("ended_at", Operator.Is, "null")
                .Filter("started_at", Operator.LessThan, ToUtcIso(cutoffUtc))
                .Order("started_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task<List<VideoParticipant>> GetStaleLiveParticipants(
            Supabase.Client supabase, Guid videoStreamId, DateTime cutoffUtc, CancellationToken ct) =>
            (await supabase
                .From<VideoParticipant>()
                .Filter("video_stream_id", Operator.Equals, videoStreamId.ToString())
                .Filter("left_at", Operator.Is, "null")
                .Filter("last_seen_at", Operator.LessThan, ToUtcIso(cutoffUtc))
                .Order("joined_at", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(ct))
            .Models;

        public async Task<int> PurgeParticipantsLeftBefore(
            Supabase.Client supabase, DateTime cutoffUtc, CancellationToken ct)
        {
            var cutoff = ToUtcIso(cutoffUtc);

            var doomed = await supabase
                .From<VideoParticipant>()
                .Filter("left_at", Operator.LessThan, cutoff)
                .Count(CountType.Exact, ct);

            if (doomed > 0)
            {
                await supabase
                    .From<VideoParticipant>()
                    .Filter("left_at", Operator.LessThan, cutoff)
                    .Delete(new QueryOptions { Returning = ReturnType.Minimal }, ct);
            }

            // Every participant of a stream that ended before the cutoff left at or
            // before ended_at, so those streams are now unreferenced.
            await supabase
                .From<VideoStream>()
                .Filter("ended_at", Operator.LessThan, cutoff)
                .Delete(new QueryOptions { Returning = ReturnType.Minimal }, ct);

            return doomed;
        }
```

Private helper (with the gates):

```csharp
        private static async Task<(VideoStream Stream, VideoParticipant Target)> GetControlledTarget(
            Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct)
        {
            var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
            var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

            if (stream.HostId != callerId && !await CanControlEventVideo(supabase, hostEvent, callerId, ct))
                throw new ForbiddenException(
                    "Only the stream host, event organizer, or group founder may change presenters.");

            var target = await FindLiveRow(supabase, stream.Id, targetUserId, ct)
                ?? throw new NotFoundException("That user is not live on this video stream.");

            return (stream, target);
        }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStream"`
Expected: PASS (29 + 15 = 44 tests).

- [ ] **Step 6: Commit**

```bash
git add AlSaqr.Data/Repositories/Meetup tests/AlSaqr.Tests/Video/VideoStreamControlTests.cs
git commit -m "feat(video): add presenter control, end, reaper queries, retention purge"
```

---

### Task 8: Reaper

**Files:**
- Create: `AlSaqr.API/HostedServices/VideoStreamReaper.cs`
- Create: `AlSaqr.API/HostedServices/VideoStreamReaperService.cs`
- Create: `tests/AlSaqr.Tests/Video/Fakes.cs`
- Test: `tests/AlSaqr.Tests/Video/VideoStreamReaperTests.cs`

**Interfaces:**
- Consumes: `IVideoStreamRepository` (Tasks 5–7), `IVideoSfuService` (Task 3), `IEventVideoBroadcaster` (Task 4).
- Produces: `IVideoStreamReaper { Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct); Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct); }`, with `VideoStreamReaper.ParticipantTimeout = 60 s`, `MaxStreamDuration = 4 h`, `RoomGrace = 5 min`, `Retention = 90 days`.
- Produces (tests, used by Task 9): `FakeVideoSfuService` (`List<string> Calls`, `List<(string Room, string Identity, bool CanPublish)> MintedTokens`, `bool FailCreateRoom`) and `FakeEventVideoBroadcaster` (`List<(string Name, Guid EventId, Guid? Subject, string? Detail)> Events`).

- [ ] **Step 1: Write the fakes**

`tests/AlSaqr.Tests/Video/Fakes.cs`:

```csharp
using AlSaqr.Infrastructure.Video;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Tests.Video
{
    /// <summary>Records SFU calls; several tests assert a call did NOT happen.</summary>
    public sealed class FakeVideoSfuService : IVideoSfuService
    {
        public List<string> Calls { get; } = new();

        public List<(string Room, string Identity, bool CanPublish)> MintedTokens { get; } = new();

        public bool FailCreateRoom { get; set; }

        public Task CreateRoomAsync(string roomName, int maxParticipants, DateTime notAfterUtc, CancellationToken ct = default)
        {
            Calls.Add($"CreateRoom {roomName} max={maxParticipants}");
            if (FailCreateRoom)
                throw new InvalidOperationException("Galène admin PUT failed with status 503.");
            return Task.CompletedTask;
        }

        public SfuJoinToken MintJoinToken(string roomName, string identity, bool canPublish)
        {
            MintedTokens.Add((roomName, identity, canPublish));
            return new SfuJoinToken($"token-for-{identity}", "ws://galene.test/ws", roomName, DateTime.UtcNow.AddSeconds(90));
        }

        public Task SetPublishAsync(string roomName, string identity, bool canPublish, CancellationToken ct = default)
        {
            Calls.Add($"SetPublish {roomName} {identity} {canPublish}");
            return Task.CompletedTask;
        }

        public Task RemoveParticipantsAsync(string roomName, IReadOnlyCollection<string> identities, CancellationToken ct = default)
        {
            Calls.Add($"Remove {roomName} {string.Join(",", identities)}");
            return Task.CompletedTask;
        }

        public Task DeleteRoomAsync(string roomName, CancellationToken ct = default)
        {
            Calls.Add($"DeleteRoom {roomName}");
            return Task.CompletedTask;
        }
    }

    public sealed class FakeEventVideoBroadcaster : IEventVideoBroadcaster
    {
        public List<(string Name, Guid EventId, Guid? Subject, string? Detail)> Events { get; } = new();

        public Task StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, CancellationToken ct = default) =>
            Record("stream_started", eventId, videoStreamId, null);

        public Task StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, CancellationToken ct = default) =>
            Record("stream_ended", eventId, videoStreamId, null);

        public Task ParticipantJoinedAsync(Guid eventId, VideoParticipantDto participant, CancellationToken ct = default) =>
            Record("participant_joined", eventId, participant.ParticipantId, participant.Username);

        public Task ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, CancellationToken ct = default) =>
            Record("participant_left", eventId, participantId, reason);

        public Task RoleChangedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default) =>
            Record("role_changed", eventId, participantId, role);

        private Task Record(string name, Guid eventId, Guid? subject, string? detail)
        {
            Events.Add((name, eventId, subject, detail));
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Write the failing tests (spec L4–L7, purge)**

`tests/AlSaqr.Tests/Video/VideoStreamReaperTests.cs`:

```csharp
using AlSaqr.API.HostedServices;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class VideoStreamReaperTests : IAsyncLifetime
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly PostgrestFixture _fx;
        private readonly VideoStreamRepository _repo = new();
        private readonly FakeVideoSfuService _sfu = new();
        private readonly FakeEventVideoBroadcaster _broadcaster = new();
        private VideoStreamReaper _reaper = default!;
        private VideoScenario _s = default!;
        private VideoStream _stream = default!;

        public VideoStreamReaperTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _reaper = new VideoStreamReaper(_repo, _sfu, _broadcaster, NullLogger<VideoStreamReaper>.Instance);
            _s = await VideoScenario.Create(_fx.Seed);
            _stream = await _repo.StartEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.JoinEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _repo.JoinEventStream(_fx.Client, _s.AttendeeId, _s.EventId, default);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private Task MakeStale(Guid userId) =>
            _fx.Seed.Execute(
                $"update {S}.video_participants set last_seen_at = now() - interval '61 seconds' where participant_id = @u",
                ("u", userId));

        private Task<string?> LeftReason(Guid userId) =>
            _fx.Seed.Scalar<string?>(
                $"select left_reason from {S}.video_participants where participant_id = @u", ("u", userId));

        [Fact]
        public async Task EvictsStaleParticipant_OnSfuAndInDb_AndBroadcasts()
        {
            await MakeStale(_s.AttendeeId);

            await _reaper.ReapOnceAsync(_fx.Client, DateTime.UtcNow, default);

            Assert.Equal("reaped", await LeftReason(_s.AttendeeId));
            Assert.Equal($"Remove {_stream.RoomName} {_stream.Id}:{_s.AttendeeId}", Assert.Single(_sfu.Calls));
            Assert.Contains(_broadcaster.Events, e => e.Name == "participant_left" && e.Subject == _s.AttendeeId && e.Detail == "reaped");
        }

        [Fact]
        public async Task DoesNotEvictHeartbeatingParticipant()
        {
            await _reaper.ReapOnceAsync(_fx.Client, DateTime.UtcNow, default);

            Assert.Null(await LeftReason(_s.AttendeeId));
            Assert.Empty(_sfu.Calls);
        }

        [Fact]
        public async Task EndsStream_WhenEveryoneIsStale_WithOneRoomDeleteAndNoSeparateKicks()
        {
            await MakeStale(_s.AttendeeId);
            await MakeStale(_s.OrganizerId);

            await _reaper.ReapOnceAsync(_fx.Client, DateTime.UtcNow, default);

            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            // Deleting the room disconnects everyone; kicking first would cost an extra operator session.
            Assert.Equal($"DeleteRoom {_stream.RoomName}", Assert.Single(_sfu.Calls));
            Assert.Contains(_broadcaster.Events, e => e.Name == "stream_ended" && e.Subject == _stream.Id);
        }

        [Fact]
        public async Task KeepsStream_WhenSomeoneIsStillHere()
        {
            await MakeStale(_s.AttendeeId);

            await _reaper.ReapOnceAsync(_fx.Client, DateTime.UtcNow, default);

            Assert.NotNull(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.DoesNotContain(_sfu.Calls, c => c.StartsWith("DeleteRoom"));
        }

        [Fact]
        public async Task EndsStreamPastFourHours_EvenWhenOccupied()
        {
            await _fx.Seed.Execute(
                $"update {S}.video_streams set started_at = now() - interval '4 hours 1 minute' where id = @s",
                ("s", _stream.Id));

            await _reaper.ReapOnceAsync(_fx.Client, DateTime.UtcNow, default);

            Assert.Equal("expired", await LeftReason(_s.AttendeeId));
            Assert.Equal("expired", await LeftReason(_s.OrganizerId));
            Assert.Null(await _repo.GetLiveEventStream(_fx.Client, _s.EventId, default));
            Assert.Equal($"DeleteRoom {_stream.RoomName}", Assert.Single(_sfu.Calls));
        }

        [Fact]
        public async Task Purge_Uses90DayRetention()
        {
            await _repo.EndEventStream(_fx.Client, _s.OrganizerId, _s.EventId, default);
            await _fx.Seed.Execute(
                $"update {S}.video_participants set left_at = now() - interval '91 days' where participant_id = @u",
                ("u", _s.AttendeeId));

            Assert.Equal(1, await _reaper.PurgeAsync(_fx.Client, DateTime.UtcNow, default));
        }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStreamReaperTests"`
Expected: build FAIL — `The type or namespace name 'VideoStreamReaper' could not be found`.

- [ ] **Step 4: Implement the reaper logic**

`AlSaqr.API/HostedServices/VideoStreamReaper.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure.Video;

namespace AlSaqr.API.HostedServices
{
    /// <summary>
    /// The three teardown guarantees of specs/video-streaming-online-events.md §7 plus
    /// the §9 retention purge. Every teardown converges on the same repository → SFU →
    /// broadcast steps the explicit leave/end paths use. Takes utcNow explicitly so
    /// tests control time; all time windows are evaluated in SQL.
    /// </summary>
    public interface IVideoStreamReaper
    {
        Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct);

        Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct);
    }

    public sealed class VideoStreamReaper : IVideoStreamReaper
    {
        public static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(60);   // C9
        public static readonly TimeSpan MaxStreamDuration = TimeSpan.FromHours(4);       // C11

        /// <summary>
        /// The SFU room's own access cutoff sits this far past MaxStreamDuration, so
        /// the reaper ends the stream first and the cutoff is only a backstop.
        /// </summary>
        public static readonly TimeSpan RoomGrace = TimeSpan.FromMinutes(5);

        public static readonly TimeSpan Retention = TimeSpan.FromDays(90);              // §9

        private readonly IVideoStreamRepository _streams;
        private readonly IVideoSfuService _sfu;
        private readonly IEventVideoBroadcaster _broadcaster;
        private readonly ILogger<VideoStreamReaper> _logger;

        public VideoStreamReaper(
            IVideoStreamRepository streams,
            IVideoSfuService sfu,
            IEventVideoBroadcaster broadcaster,
            ILogger<VideoStreamReaper> logger)
        {
            _streams = streams;
            _sfu = sfu;
            _broadcaster = broadcaster;
            _logger = logger;
        }

        public async Task ReapOnceAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct)
        {
            var expired = (await _streams.GetLiveStreamsStartedBefore(supabase, utcNow - MaxStreamDuration, ct))
                .Select(s => s.Id)
                .ToHashSet();

            foreach (var stream in await _streams.GetLiveStreams(supabase, ct))
            {
                var isExpired = expired.Contains(stream.Id);
                var reason = isExpired ? VideoParticipant.LeftExpired : VideoParticipant.LeftReaped;

                // C11 removes everyone; otherwise only participants silent past the timeout (C9).
                var doomed = isExpired
                    ? await _streams.GetLiveParticipants(supabase, stream.Id, ct)
                    : await _streams.GetStaleLiveParticipants(supabase, stream.Id, utcNow - ParticipantTimeout, ct);

                var identities = new List<string>();
                foreach (var participant in doomed)
                {
                    if (!string.IsNullOrEmpty(participant.SfuIdentity))
                        identities.Add(participant.SfuIdentity);

                    await _streams.MarkParticipantLeft(supabase, participant, reason, ct);
                    await _broadcaster.ParticipantLeftAsync(stream.EventId, participant.ParticipantId, reason, ct);

                    _logger.LogInformation(
                        "Removed participant {UserId} from video stream {StreamId} ({Reason}).",
                        participant.ParticipantId, stream.Id, reason);
                }

                var remaining = isExpired ? 0 : (await _streams.GetLiveParticipants(supabase, stream.Id, ct)).Count;
                if (remaining > 0)
                {
                    // One operator session per room per scan, not one per participant.
                    if (identities.Count > 0)
                        await _sfu.RemoveParticipantsAsync(stream.RoomName, identities, ct);
                    continue;
                }

                // Deleting the room disconnects everyone, so no separate kicks are needed.
                var ended = await _streams.EndStreamSystem(supabase, stream, ct);
                await _sfu.DeleteRoomAsync(ended.RoomName, ct);
                await _broadcaster.StreamEndedAsync(ended.EventId, ended.Id, ended.EndedAt!.Value, ct);

                _logger.LogInformation(
                    "Reaper ended video stream {StreamId} ({Why}).", ended.Id, isExpired ? "4-hour limit" : "empty");
            }
        }

        public Task<int> PurgeAsync(Supabase.Client supabase, DateTime utcNow, CancellationToken ct) =>
            _streams.PurgeParticipantsLeftBefore(supabase, utcNow - Retention, ct);
    }
}
```

- [ ] **Step 5: Implement the hosted loop**

`AlSaqr.API/HostedServices/VideoStreamReaperService.cs`:

```csharp
namespace AlSaqr.API.HostedServices
{
    /// <summary>
    /// Runs <see cref="IVideoStreamReaper"/> every 30 s and the retention purge once
    /// a day (specs/video-streaming-online-events.md §7, §9). Mirrors
    /// SpaceReaperService, which stays untouched.
    /// </summary>
    public sealed class VideoStreamReaperService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PurgeInterval = TimeSpan.FromDays(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Supabase.Client _supabase;
        private readonly ILogger<VideoStreamReaperService> _logger;

        public VideoStreamReaperService(
            IServiceScopeFactory scopeFactory,
            Supabase.Client supabase,
            ILogger<VideoStreamReaperService> logger)
        {
            _scopeFactory = scopeFactory;
            _supabase = supabase;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ScanInterval);
            var lastPurge = DateTime.MinValue;

            while (await WaitForNextTick(timer, stoppingToken))
            {
                try
                {
                    // Scoped repository + typed HttpClients: resolve per tick so handler
                    // rotation keeps working instead of pinning one handler here.
                    using var scope = _scopeFactory.CreateScope();
                    var reaper = scope.ServiceProvider.GetRequiredService<IVideoStreamReaper>();
                    var now = DateTime.UtcNow;

                    await reaper.ReapOnceAsync(_supabase, now, stoppingToken);

                    if (now - lastPurge >= PurgeInterval)
                    {
                        var purged = await reaper.PurgeAsync(_supabase, now, stoppingToken);
                        lastPurge = now;
                        _logger.LogInformation("Purged {Count} video participation rows past retention.", purged);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Survive transient Supabase/Galène failures; retry next tick.
                    _logger.LogError(ex, "Video stream reaper scan failed; retrying next tick.");
                }
            }
        }

        private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken ct)
        {
            try
            {
                return await timer.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~VideoStreamReaperTests"`
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add AlSaqr.API/HostedServices/VideoStreamReaper.cs AlSaqr.API/HostedServices/VideoStreamReaperService.cs tests/AlSaqr.Tests/Video
git commit -m "feat(video): add stream reaper and retention purge"
```

---

### Task 9: Controller

**Files:**
- Create: `AlSaqr.API/Controllers/Meetup/EventVideoController.cs`
- Create: `tests/AlSaqr.Tests/Video/EventVideoApi.cs`
- Test: `tests/AlSaqr.Tests/Video/EventVideoControllerTests.cs`

**Interfaces:**
- Consumes: `IAccessTokenVerifier` (Plan A); repository (Tasks 5–7); `IVideoSfuService`, `GaleneConfig` (Task 3); `IEventVideoBroadcaster` (Task 4); `VideoStreamReaper.MaxStreamDuration`/`RoomGrace` (Task 8); fakes (Task 8).
- Produces: routes under `api/EventVideo/event/{eventId}/…`: `GET live`, `POST start|join|token|leave|end|heartbeat|camera`, `POST presenters/{userId}/promote|demote`.

- [ ] **Step 1: Write the test host**

`tests/AlSaqr.Tests/Video/EventVideoApi.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AlSaqr.API.Controllers.Meetup;
using AlSaqr.API.Extensions;
using AlSaqr.API.Middleware;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlSaqr.Tests.Video
{
    /// <summary>
    /// The real controller, repository, verifier and middleware over the PostgREST
    /// fixture, with the SFU and Realtime faked.
    /// </summary>
    public sealed class EventVideoApi : IAsyncDisposable
    {
        public const string TokenSecret = "test-access-token-secret-0123456789-abcdefghij";

        private readonly IHost _host;

        private EventVideoApi(IHost host, FakeVideoSfuService sfu, FakeEventVideoBroadcaster broadcaster)
        {
            _host = host;
            Sfu = sfu;
            Broadcaster = broadcaster;
            Http = host.GetTestClient();
        }

        public HttpClient Http { get; }

        public FakeVideoSfuService Sfu { get; }

        public FakeEventVideoBroadcaster Broadcaster { get; }

        public static async Task<EventVideoApi> StartAsync(Supabase.Client supabase)
        {
            var sfu = new FakeVideoSfuService();
            var broadcaster = new FakeEventVideoBroadcaster();

            var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services
                            .AddControllers(o => o.UseRoutePrefix("api"))
                            .AddApplicationPart(typeof(EventVideoController).Assembly);
                        services.AddSingleton(supabase);
                        services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
                        services.AddSingleton<IVideoSfuService>(sfu);
                        services.AddSingleton<IEventVideoBroadcaster>(broadcaster);
                        services.AddSingleton<IAccessTokenVerifier>(new AccessTokenVerifier(TokenSecret));
                        services.Configure<GaleneConfig>(c =>
                        {
                            c.MaxParticipants = 50;
                            c.MaxPresenters = 9;
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .StartAsync();

            return new EventVideoApi(host, sfu, broadcaster);
        }

        public static string Token(Guid userId) =>
            new TokenService(TokenSecret).GenerateTokens(userId.ToString()).AccessToken;

        public Task<HttpResponseMessage> Post(string path, Guid asUser, object? body = null) =>
            Send(HttpMethod.Post, path, Token(asUser), body ?? new { });

        public Task<HttpResponseMessage> Get(string path, Guid asUser) =>
            Send(HttpMethod.Get, path, Token(asUser), null);

        public Task<HttpResponseMessage> PostWithRawToken(string path, string token) =>
            Send(HttpMethod.Post, path, token, new { });

        private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null)
                request.Content = JsonContent.Create(body);
            return Http.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/AlSaqr.Tests/Video/EventVideoControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AlSaqr.Infrastructure;
using AlSaqr.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Meetup.EventVideo;

namespace AlSaqr.Tests.Video
{
    [Collection(PostgrestCollection.Name)]
    public class EventVideoControllerTests : IAsyncLifetime
    {
        private readonly PostgrestFixture _fx;
        private EventVideoApi _api = default!;
        private VideoScenario _s = default!;

        public EventVideoControllerTests(PostgrestFixture fx) => _fx = fx;

        public async Task InitializeAsync()
        {
            await _fx.ResetAsync();
            _api = await EventVideoApi.StartAsync(_fx.Client);
            _s = await VideoScenario.Create(_fx.Seed);
        }

        public async Task DisposeAsync() => await _api.DisposeAsync();

        private string Route(string action) => $"/api/EventVideo/event/{_s.EventId}/{action}";

        private async Task Start()
        {
            var response = await _api.Post(Route("start"), _s.OrganizerId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Start_ByOrganizer_CreatesRoomWithCostGuards_AndBroadcasts()
        {
            var response = await _api.Post(Route("start"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var stream = await response.Content.ReadFromJsonAsync<VideoStreamToDisplay>();
            Assert.True(stream!.IsLive);
            Assert.Equal($"CreateRoom stream-{stream.VideoStreamId} max=50", Assert.Single(_api.Sfu.Calls));
            Assert.Contains(_api.Broadcaster.Events, e => e.Name == "stream_started" && e.Subject == stream.VideoStreamId);
        }

        [Fact]
        public async Task Start_InPersonEvent_Returns400ProblemDetails_AndMakesNoSfuCall()
        {
            var inPerson = await _fx.Seed.Event(_s.GroupId, isOnline: false);

            var response = await _api.Post($"/api/EventVideo/event/{inPerson}/start", _s.FounderId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Empty(_api.Sfu.Calls);
        }

        [Fact]
        public async Task Start_WhenCreateRoomFails_Returns500_AndLeavesNoLiveStream()
        {
            _api.Sfu.FailCreateRoom = true;

            var response = await _api.Post(Route("start"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            // No live stream: Ok(null) is written as 204 by ASP.NET's no-content formatter.
            var live = await _api.Get(Route("live"), _s.OrganizerId);
            Assert.Equal(HttpStatusCode.NoContent, live.StatusCode);

            _api.Sfu.FailCreateRoom = false;
            Assert.Equal(HttpStatusCode.OK, (await _api.Post(Route("start"), _s.OrganizerId)).StatusCode);
        }

        [Fact]
        public async Task Join_WithForgedToken_Returns401_AndMintsNoToken()
        {
            await Start();
            var forged = new TokenService("some-other-secret-0123456789-abcdefghijklmn")
                .GenerateTokens(_s.AttendeeId.ToString()).AccessToken;

            var response = await _api.PostWithRawToken(Route("join"), forged);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Join_NonAttendee_Returns403ProblemDetails_AndMintsNoToken()
        {
            await Start();

            var response = await _api.Post(Route("join"), _s.OutsiderId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(403, problem!.Status);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Join_Attendee_ReturnsViewerToken_RosterAndFullBroadcast()
        {
            await Start();

            var response = await _api.Post(Route("join"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JoinVideoStreamResultDto>();
            Assert.Equal("viewer", result!.Role);
            Assert.False(result.CanPublish);
            Assert.Equal("ws://galene.test/ws", result.WsUrl);
            Assert.Equal($"stream-{result.Stream.VideoStreamId}", result.Group);
            Assert.Contains(result.Participants, p => p.ParticipantId == _s.AttendeeId && p.Username == "attendee");
            var minted = Assert.Single(_api.Sfu.MintedTokens);
            Assert.Equal($"{result.Stream.VideoStreamId}:{_s.AttendeeId}", minted.Identity);
            Assert.False(minted.CanPublish);
            Assert.DoesNotContain(_api.Sfu.Calls, c => c.StartsWith("Remove"));
            // The broadcast carries the roster entry, so other clients need no refetch.
            Assert.Contains(_api.Broadcaster.Events,
                e => e.Name == "participant_joined" && e.Subject == _s.AttendeeId && e.Detail == "attendee");
        }

        [Fact]
        public async Task Join_Again_KicksOlderConnection()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var again = await _api.Post(Route("join"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            var kick = Assert.Single(_api.Sfu.Calls);
            Assert.StartsWith("Remove stream-", kick);
            Assert.EndsWith($":{_s.AttendeeId}", kick);
            Assert.Equal(2, _api.Sfu.MintedTokens.Count);
        }

        [Fact]
        public async Task Token_ForLiveParticipant_ReturnsFreshToken_WithoutBroadcast()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            var eventsBefore = _api.Broadcaster.Events.Count;

            var response = await _api.Post(Route("token"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var token = await response.Content.ReadFromJsonAsync<VideoTokenDto>();
            Assert.Equal("viewer", token!.Role);
            Assert.False(token.CanPublish);
            Assert.False(string.IsNullOrEmpty(token.Token));
            Assert.StartsWith("stream-", token.Group);
            Assert.Equal(eventsBefore, _api.Broadcaster.Events.Count);
        }

        [Fact]
        public async Task Token_WhenNotLive_Returns403()
        {
            await Start();

            var response = await _api.Post(Route("token"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(_api.Sfu.MintedTokens);
        }

        [Fact]
        public async Task Leave_RemovesOnSfu_AndBroadcasts_SecondLeaveIsNoOp()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var first = await _api.Post(Route("leave"), _s.AttendeeId);
            var second = await _api.Post(Route("leave"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
            Assert.Single(_api.Sfu.Calls, c => c.StartsWith("Remove") && c.EndsWith($":{_s.AttendeeId}"));
            Assert.Single(_api.Broadcaster.Events, e => e.Name == "participant_left" && e.Detail == "explicit");
        }

        [Fact]
        public async Task Promote_GrantsPublishOnSfu_ThenDemote_RevokesIt()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);
            _api.Sfu.Calls.Clear();

            var promote = await _api.Post(Route($"presenters/{_s.AttendeeId}/promote"), _s.OrganizerId);
            var demote = await _api.Post(Route($"presenters/{_s.AttendeeId}/demote"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, demote.StatusCode);
            Assert.Collection(_api.Sfu.Calls,
                c => Assert.EndsWith($":{_s.AttendeeId} True", c),
                c => Assert.EndsWith($":{_s.AttendeeId} False", c));
            Assert.Equal(2, _api.Broadcaster.Events.Count(e => e.Name == "role_changed"));
        }

        [Fact]
        public async Task End_DeletesRoom_AndBroadcastsStreamEnded()
        {
            await Start();
            _api.Sfu.Calls.Clear();

            var response = await _api.Post(Route("end"), _s.OrganizerId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.StartsWith("DeleteRoom stream-", Assert.Single(_api.Sfu.Calls));
            Assert.Contains(_api.Broadcaster.Events, e => e.Name == "stream_ended");
        }

        [Fact]
        public async Task Heartbeat_WhenNotLive_Returns403()
        {
            await Start();

            var response = await _api.Post(Route("heartbeat"), _s.AttendeeId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Camera_PersistsStateForCaller()
        {
            await Start();
            await _api.Post(Route("join"), _s.AttendeeId);

            var response = await _api.Post(Route("camera"), _s.AttendeeId,
                new { values = new { cameraEnabled = true, muted = false } });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(await _fx.Seed.Scalar<bool>(
                "select camera_enabled from \"alsaqr-2026\".video_participants where participant_id = @u",
                ("u", _s.AttendeeId)));
        }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoControllerTests"`
Expected: build FAIL — `The type or namespace name 'EventVideoController' could not be found`.

- [ ] **Step 4: Implement**

`AlSaqr.API/Controllers/Meetup/EventVideoController.cs`:

```csharp
using AlSaqr.API.HostedServices;
using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static AlSaqr.Domain.Meetup.EventVideo;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.Meetup
{
    /// <summary>
    /// Live video for online events (specs/video-streaming-online-events.md §8). Every
    /// route is event-scoped: one stream per event is live. Authorization is decided
    /// by the repository before any SFU call or token mint; failures surface as
    /// repository exceptions mapped by ExceptionHandlingMiddleware. Responses are bare
    /// DTOs (spec §8 envelope decision). Nothing here is cached: it is real-time state.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class EventVideoController : AuthorizedControllerBase
    {
        private readonly Supabase.Client _supabase;
        private readonly IVideoStreamRepository _videoStreams;
        private readonly IVideoSfuService _sfu;
        private readonly IEventVideoBroadcaster _broadcaster;
        private readonly IAccessTokenVerifier _tokens;
        private readonly GaleneConfig _config;

        public EventVideoController(
            Supabase.Client supabase,
            IVideoStreamRepository videoStreams,
            IVideoSfuService sfu,
            IEventVideoBroadcaster broadcaster,
            IAccessTokenVerifier tokens,
            IOptions<GaleneConfig> config)
        {
            _supabase = supabase;
            _videoStreams = videoStreams;
            _sfu = sfu;
            _broadcaster = broadcaster;
            _tokens = tokens;
            _config = config.Value;
        }

        /// <summary>200 with the live stream, or 204 when none is live.</summary>
        [HttpGet("event/{eventId}/live")]
        public async Task<IActionResult> GetLiveStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out _);
            if (authError != null)
                return authError;

            return Ok(await _videoStreams.GetLiveEventStream(_supabase, eventId, HttpContext.RequestAborted));
        }

        [HttpPost("event/{eventId}/start")]
        public async Task<IActionResult> StartStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var ct = HttpContext.RequestAborted;
            var stream = await _videoStreams.StartEventStream(_supabase, callerId, eventId, ct);

            try
            {
                // The room's own cutoff backs the reaper's 4-hour limit (C11).
                var notAfter = DateTime.UtcNow + VideoStreamReaper.MaxStreamDuration + VideoStreamReaper.RoomGrace;
                await _sfu.CreateRoomAsync(stream.RoomName, _config.MaxParticipants, notAfter, ct);
            }
            catch
            {
                // Compensate, then let the middleware report the failure: a live row with
                // no room would 409 every retry until the reaper noticed.
                await _videoStreams.EndStreamSystem(_supabase, stream, CancellationToken.None);
                throw;
            }

            await _broadcaster.StreamStartedAsync(eventId, stream.Id, stream.StartedAt, ct);

            return Ok(await _videoStreams.RequireLiveEventStream(_supabase, eventId, ct));
        }

        /// <summary>
        /// Joins the live stream. Returns the stream, the caller's role, the roster and
        /// a short-lived room token: everything the client needs to connect and render
        /// in one round trip. A non-attendee is rejected before any token is minted.
        /// </summary>
        [HttpPost("event/{eventId}/join")]
        public async Task<IActionResult> JoinStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var ct = HttpContext.RequestAborted;
            var (stream, participant, hadLiveConnection) =
                await _videoStreams.JoinEventStream(_supabase, callerId, eventId, ct);
            var identity = participant.SfuIdentity!;

            // One seat per user: Galène lets two connections share a username, so a
            // second tab or a reload replaces the older connection here.
            if (hadLiveConnection)
                await _sfu.RemoveParticipantsAsync(stream.RoomName, new[] { identity }, ct);

            var canPublish = participant.Role != VideoParticipant.RoleViewer;   // C4
            var token = _sfu.MintJoinToken(stream.RoomName, identity, canPublish);

            var displayTask = _videoStreams.RequireLiveEventStream(_supabase, eventId, ct);
            var rosterTask = _videoStreams.GetParticipants(_supabase, stream.Id, ct);
            await Task.WhenAll(displayTask, rosterTask);
            var roster = await rosterTask;

            await _broadcaster.ParticipantJoinedAsync(eventId, roster.Single(p => p.ParticipantId == callerId), ct);

            return Ok(new JoinVideoStreamResultDto
            {
                Stream = await displayTask,
                Role = participant.Role,
                WsUrl = token.WsUrl,
                Group = token.Group,
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                CanPublish = canPublish,
                Participants = roster,
            });
        }

        /// <summary>
        /// A fresh room token for someone already live (slow first connect, or a
        /// reconnect that outlasted the 90 s token). No broadcast, no roster.
        /// 403 means the reaper removed the caller: join again.
        /// </summary>
        [HttpPost("event/{eventId}/token")]
        public async Task<IActionResult> RefreshToken(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var (stream, participant) = await _videoStreams.TouchLiveParticipant(
                _supabase, callerId, eventId, HttpContext.RequestAborted);

            var canPublish = participant.Role != VideoParticipant.RoleViewer;
            var token = _sfu.MintJoinToken(
                stream.RoomName, VideoParticipant.SfuIdentityFor(stream.Id, callerId), canPublish);

            return Ok(new VideoTokenDto
            {
                Role = participant.Role,
                WsUrl = token.WsUrl,
                Group = token.Group,
                Token = token.Token,
                ExpiresAt = token.ExpiresAt,
                CanPublish = canPublish,
            });
        }

        /// <summary>
        /// Leaves the stream: disconnects the caller on the SFU and announces it.
        /// Idempotent, so every client exit path can call it (with fetch keepalive).
        /// </summary>
        [HttpPost("event/{eventId}/leave")]
        public async Task<IActionResult> LeaveStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var ct = HttpContext.RequestAborted;
            var left = await _videoStreams.LeaveEventStream(
                _supabase, callerId, eventId, VideoParticipant.LeftExplicit, ct);

            if (left.HasValue)
            {
                var (stream, snapshot) = left.Value;

                // Server-side removal is what stops the media (C9).
                if (!string.IsNullOrEmpty(snapshot.SfuIdentity))
                    await _sfu.RemoveParticipantsAsync(stream.RoomName, new[] { snapshot.SfuIdentity }, ct);

                await _broadcaster.ParticipantLeftAsync(eventId, callerId, VideoParticipant.LeftExplicit, ct);
            }

            return NoContent();
        }

        [HttpPost("event/{eventId}/end")]
        public async Task<IActionResult> EndStream(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var ct = HttpContext.RequestAborted;
            var ended = await _videoStreams.EndEventStream(_supabase, callerId, eventId, ct);

            await _sfu.DeleteRoomAsync(ended.RoomName, ct);
            await _broadcaster.StreamEndedAsync(eventId, ended.Id, ended.EndedAt!.Value, ct);

            return NoContent();
        }

        /// <summary>Clients call this every ~20 s, well under the 60 s reaper timeout.</summary>
        [HttpPost("event/{eventId}/heartbeat")]
        public async Task<IActionResult> Heartbeat(Guid eventId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            await _videoStreams.TouchLiveParticipant(_supabase, callerId, eventId, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpPost("event/{eventId}/camera")]
        public async Task<IActionResult> SetCameraState(
            Guid eventId, [FromBody] AlSaqrUpsertRequest<CameraStateForm> request)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            if (request?.Values == null)
                return BadRequest("Missing required fields");

            await _videoStreams.SetCameraState(
                _supabase, callerId, eventId, request.Values.CameraEnabled, request.Values.Muted,
                HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpPost("event/{eventId}/presenters/{userId}/promote")]
        public async Task<IActionResult> PromotePresenter(Guid eventId, Guid userId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var ct = HttpContext.RequestAborted;
            var (stream, promoted) = await _videoStreams.PromotePresenter(
                _supabase, callerId, eventId, userId, _config.MaxPresenters, ct);

            if (!string.IsNullOrEmpty(promoted.SfuIdentity))
                await _sfu.SetPublishAsync(stream.RoomName, promoted.SfuIdentity, canPublish: true, ct);

            await _broadcaster.RoleChangedAsync(eventId, userId, promoted.Role, ct);
            return NoContent();
        }

        /// <summary>
        /// Presenter → viewer. Flipping the stored role is not enough: the live
        /// connection still has publish rights. SetPublishAsync revokes them on the SFU,
        /// effective immediately.
        /// </summary>
        [HttpPost("event/{eventId}/presenters/{userId}/demote")]
        public async Task<IActionResult> DemotePresenter(Guid eventId, Guid userId)
        {
            var authError = AuthorizeCaller(out var callerId);
            if (authError != null)
                return authError;

            var ct = HttpContext.RequestAborted;
            var (stream, demoted) = await _videoStreams.DemotePresenter(_supabase, callerId, eventId, userId, ct);

            if (!string.IsNullOrEmpty(demoted.SfuIdentity))
                await _sfu.SetPublishAsync(stream.RoomName, demoted.SfuIdentity, canPublish: false, ct);

            await _broadcaster.RoleChangedAsync(eventId, userId, demoted.Role, ct);
            return NoContent();
        }

        /// <summary>
        /// Presence/expiry gate, then the signature-verified caller id. Identity never
        /// comes from the shared logged-in-user cache slot (spec §0 DEP-2/DEP-3).
        /// </summary>
        private IActionResult? AuthorizeCaller(out Guid callerId)
        {
            callerId = Guid.Empty;

            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            callerId = _tokens.GetVerifiedUserId(Request.Headers.Authorization.ToString());
            return callerId == Guid.Empty ? Unauthorized("A verified access token is required.") : null;
        }
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoControllerTests"`
Expected: PASS (14 tests).

- [ ] **Step 6: Commit**

```bash
git add AlSaqr.API/Controllers/Meetup/EventVideoController.cs tests/AlSaqr.Tests/Video
git commit -m "feat(video): add EventVideo controller with token refresh"
```

---

### Task 10: Wiring, configuration, local Galène, and the no-recording guard

**Files:**
- Create: `AlSaqr.API/Extensions/EventVideoServiceCollectionExtensions.cs`
- Modify: `AlSaqr.API/Program.cs` (after the audio-spaces block)
- Modify: `AlSaqr.API/Config/AppSecrets.cs`
- Modify: `AlSaqr.API/appsettings.Development.json`
- Create: `docker-compose.galene.yml`
- Test: `tests/AlSaqr.Tests/Video/EventVideoWiringTests.cs`

**Interfaces:**
- Produces: `IServiceCollection AddEventVideo(this IServiceCollection services, IConfiguration configuration)`, in namespace `AlSaqr.API.Extensions`.

- [ ] **Step 1: Write the failing tests (DI graph, config validation, spec C3)**

`tests/AlSaqr.Tests/Video/EventVideoWiringTests.cs`:

```csharp
using System.Text.RegularExpressions;
using AlSaqr.API.Controllers.Meetup;
using AlSaqr.API.Extensions;
using AlSaqr.API.HostedServices;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AlSaqr.Tests.Video
{
    public class EventVideoWiringTests
    {
        private static ServiceProvider Build(Dictionary<string, string?> settings)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new Supabase.Client("http://supabase.test", null));
            services.AddSingleton<IAccessTokenVerifier>(new AccessTokenVerifier(EventVideoApi.TokenSecret));
            services.Configure<SupabaseRealtimeConfig>(configuration.GetSection("Supabase"));   // Program.cs already does this

            services.AddEventVideo(configuration);

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }

        private static Dictionary<string, string?> ValidSettings() => new()
        {
            ["Galene:AdminUrl"] = "http://galene.internal:8443",
            ["Galene:PublicUrl"] = "https://video.test",
            ["Galene:AdminUsername"] = "alsaqr-admin",
            ["Galene:AdminPassword"] = "admin-password",
            ["Galene:TokenSecret"] = "test-galene-token-secret-0123456789-abcdef",
            ["Supabase:Url"] = "http://supabase.test",
            ["Supabase:ServiceRoleSecret"] = "s",
        };

        [Fact]
        public void AddEventVideo_BuildsAValidGraph_ForControllerAndReaper()
        {
            using var provider = Build(ValidSettings());
            using var scope = provider.CreateScope();

            Assert.NotNull(ActivatorUtilities.CreateInstance<EventVideoController>(scope.ServiceProvider));
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IVideoStreamReaper>());
            Assert.Contains(provider.GetServices<IHostedService>(), s => s is VideoStreamReaperService);
        }

        [Theory]
        [InlineData("Galene:TokenSecret", "too-short")]
        [InlineData("Galene:AdminPassword", "")]
        [InlineData("Galene:PublicUrl", "")]
        public void InvalidGaleneConfig_FailsValidation(string key, string value)
        {
            var settings = ValidSettings();
            settings[key] = value;
            using var provider = Build(settings);

            // ValidateOnStart makes the host fail at boot with this same exception.
            Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<GaleneConfig>>().Value);
        }

        [Fact]
        public void NoRecordingPermissionOrActionExistsAnywhere()
        {
            // C10: no token may carry "record", no group action may start recording,
            // and no group may be created with recording allowed.
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "AlSaqr.API.sln")))
                root = root.Parent!;

            var offenders = Directory
                .EnumerateFiles(root.FullName, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}"))
                .Where(f => Regex.IsMatch(File.ReadAllText(f), "\"(record|unrecord)\"|AllowRecording\\s*=\\s*true"))
                .ToList();

            Assert.Empty(offenders);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoWiringTests"`
Expected: build FAIL — `'IServiceCollection' does not contain a definition for 'AddEventVideo'`.

- [ ] **Step 3: Implement the extension**

`AlSaqr.API/Extensions/EventVideoServiceCollectionExtensions.cs`:

```csharp
using AlSaqr.API.HostedServices;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.Video;

namespace AlSaqr.API.Extensions
{
    public static class EventVideoServiceCollectionExtensions
    {
        /// <summary>
        /// Online-event video streaming: repository, self-hosted Galène control plane,
        /// realtime broadcaster, and the reaper/purge host. Requires
        /// IAccessTokenVerifier, Supabase.Client and SupabaseRealtimeConfig to be
        /// registered already. A missing or weak Galène secret fails at startup, not
        /// on the first call.
        /// </summary>
        public static IServiceCollection AddEventVideo(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<GaleneConfig>()
                .Bind(configuration.GetSection("Galene"))
                .Validate(c => c.TokenSecret?.Length >= 32, "Galene:TokenSecret must be at least 32 characters.")
                .Validate(c => !string.IsNullOrWhiteSpace(c.AdminUsername) && !string.IsNullOrWhiteSpace(c.AdminPassword),
                    "Galene:AdminUsername and Galene:AdminPassword are required.")
                .Validate(c => Uri.IsWellFormedUriString(c.AdminUrl, UriKind.Absolute)
                            && Uri.IsWellFormedUriString(c.PublicUrl, UriKind.Absolute),
                    "Galene:AdminUrl and Galene:PublicUrl must be absolute URLs.")
                .ValidateOnStart();

            services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
            services.AddSingleton<IGaleneOperator, GaleneOperator>();
            services.AddHttpClient<IVideoSfuService, GaleneVideoService>();
            services.AddHttpClient<ISupabaseBroadcastClient, SupabaseBroadcastClient>();
            services.AddScoped<IEventVideoBroadcaster, EventVideoBroadcaster>();
            services.AddScoped<IVideoStreamReaper, VideoStreamReaper>();
            services.AddHostedService<VideoStreamReaperService>();
            return services;
        }
    }
}
```

- [ ] **Step 4: Register in Program.cs**

In `AlSaqr.API/Program.cs`, directly after the line `builder.Services.AddHostedService<SpaceReaperService>();`, add:

```csharp

builder.Services.AddEventVideo(builder.Configuration);
```

(`AlSaqr.API.Extensions` is already imported at the top of `Program.cs`.)

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~EventVideoWiringTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Configuration and local Galène**

In `AlSaqr.API/Config/AppSecrets.cs`, add `public GaleneSettings Galene { get; set; } = new();` to `AppSecrets` after `CloudflareCalls`, and this class at the end of the namespace:

```csharp
    /// <summary>
    /// Self-hosted Galène SFU for event video. In AWS Secrets Manager as
    /// Galene__AdminUrl, Galene__PublicUrl, Galene__AdminUsername,
    /// Galene__AdminPassword, Galene__TokenSecret (≥32 chars). The password and
    /// secret never leave the server.
    /// </summary>
    public sealed class GaleneSettings
    {
        public string AdminUrl { get; set; } = default!;
        public string PublicUrl { get; set; } = default!;
        public string AdminUsername { get; set; } = default!;
        public string AdminPassword { get; set; } = default!;
        public string TokenSecret { get; set; } = default!;
        public int MaxParticipants { get; set; } = 50;
        public int MaxPresenters { get; set; } = 9;
    }
```

In `AlSaqr.API/appsettings.Development.json`, add a top-level `"Galene"` section (local-only values matching `docker/galene/dev/config.json`; production comes from AWS Secrets Manager, which overrides these):

```json
  "Galene": {
    "AdminUrl": "http://localhost:8443",
    "PublicUrl": "http://localhost:8443",
    "AdminUsername": "alsaqr-admin",
    "AdminPassword": "alsaqr-local-galene-admin",
    "TokenSecret": "alsaqr-local-galene-token-secret-0123456789",
    "MaxParticipants": 50,
    "MaxPresenters": 9
  }
```

Create `docker-compose.galene.yml` at the repo root:

```yaml
# Local dev only: self-hosted Galène SFU for event video.
# Run: docker compose -f docker-compose.galene.yml up --build
# Admin credentials match appsettings.Development.json and docker/galene/dev/config.json.
services:
  galene:
    build: ./docker/galene
    command: ["-insecure", "-http", ":8443", "-udp-range", "40000-40100"]
    ports:
      - "8443:8443"                     # HTTP + WebSocket + admin API
      - "1194:1194/tcp"                 # built-in TURN
      - "1194:1194/udp"
      - "40000-40100:40000-40100/udp"   # WebRTC media
    volumes:
      - ./docker/galene/dev/config.json:/srv/galene/data/config.json:ro
      - galene-groups:/srv/galene/groups
volumes:
  galene-groups: {}
```

(If Task 3 Step 1 found different flag names, mirror them here.)

- [ ] **Step 7: Full verification**

Run: `dotnet build AlSaqr.API.sln -c Release; dotnet test AlSaqr.API.sln -c Release --no-build`
Expected: `0 Error(s)`; all tests PASS (Plan A's 19 + Plan B's 104 = 123).

Run: `git diff --name-only main -- AlSaqr.API/Controllers/SocialMedia/SpacesController.cs AlSaqr.Data/Repositories/SocialMedia/SpaceRepository.cs AlSaqr.Infrastructure/Spaces AlSaqr.API/HostedServices/SpaceReaperService.cs`
Expected: no output (audio spaces untouched).

Run: `grep -rnE "Newtonsoft|_ = [A-Za-z.]+Async\(" --include=*.cs AlSaqr.Infrastructure/Video AlSaqr.Data/Repositories/Meetup/VideoStreamRepository.cs AlSaqr.API/Controllers/Meetup/EventVideoController.cs AlSaqr.API/HostedServices/VideoStream*.cs`
Expected: no output.

- [ ] **Step 8: Commit**

```bash
git add AlSaqr.API/Extensions/EventVideoServiceCollectionExtensions.cs AlSaqr.API/Program.cs AlSaqr.API/Config/AppSecrets.cs AlSaqr.API/appsettings.Development.json docker-compose.galene.yml tests/AlSaqr.Tests/Video/EventVideoWiringTests.cs
git commit -m "feat(video): wire event video services, config validation, and local Galène"
```

---

## After merge (not code)

- **Production Galène host.** Per PRD §5.2, run Galène on a bandwidth-included VM (~$50/mo, 20 TB), not metered cloud egress. Galène's CPU needs are small (~20 meeting participants per core), so a 4-core VM covers the 50-participant cap with headroom. Open 443/tcp (TLS in front of Galène's 8443, for `wss://`), 1194/tcp+udp (built-in TURN), and the configured UDP media range. Mount a production `config.json` with a hashed admin password and `writableGroups: true`; set `canonicalHost` if Task 3's audience-host test required it. Keep the admin API reachable only from the API's network (`AdminUrl`), never from the public internet.
- **Secrets.** Add the five `Galene__*` keys to AWS Secrets Manager. Startup validation (Task 10) rejects a missing password or a secret shorter than 32 characters.
- **Verify TURN once on a real network:** join from a phone on mobile data. That is the path the built-in TURN server exists for, and no container test exercises it.
- **Frontend:** implement the "Client contract (perceived performance)" section above with Galène's JavaScript client (`protocol.js` from the Galène repo): join with `{ group, token }` over `wsUrl`, enable simulcast, heartbeat every 20 s, hide `alsaqr-operator`.
- **Spec and PRD.** The spec's 2026-10-04 addendum records the Galène decision. PRD §5.1 still names LiveKit in two constraint rows; update them when the PRD is next revised.
- **Open decisions from the PRD**, unresolved by engineering: the under-18 policy (PRD §5.3, legal), and the pre-launch attendance query that sizes the presenter cap (PRD Issue 2).
