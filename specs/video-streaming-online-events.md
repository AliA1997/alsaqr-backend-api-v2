# Specification: Video Streaming for Online Events (Meetup)

> **Scope:** live video calls for **online events only** (`events.is_online = true`).
> Conformance keywords (**MUST**, **MUST NOT**, **SHOULD**, **MAY**) follow RFC 2119.
> This document governs a new, self-contained feature; it is written against the
> project constitution (`CLAUDE.md`) and mirrors the architecture already proven by
> `specs/audio-spaces.md`.
>
> **Constraint compliance (explicit).** The audio-spaces implementation is
> **frozen**: this feature **MUST NOT** edit `SpacesController.cs`, `Space`,
> `SpaceParticipant`, `SpaceRepository`, `CloudflareCallsService`, or
> `SpaceEventBroadcaster`, and **MUST NOT** add routes to the `Spaces` controller.
> Every artifact below is a *new* file. Audio spaces are the **reference pattern**,
> not a dependency — the two features share no code and no tables.

---

## Overview

An online event gets one live, **ephemeral** video room:

- The event's **organizer** starts the stream and becomes its **host**. Any user
  attending that event joins as a **viewer**; the host may promote a viewer to
  **presenter** (may turn on camera/mic) and demote them back.
- **Media** is WebRTC video/audio through **LiveKit** (Apache-2.0, self-hosted —
  see *SFU choice* below). The backend is a pure **control plane**: it mints
  short-lived, room-scoped join tokens and drives the LiveKit *server* API. The
  LiveKit **API secret never reaches the browser**, and the browser never receives
  a token for a room it is not authorized to be in.
- **Presence** is the Supabase realtime channel `event_video:{eventId}` (presence +
  broadcast). Presence is Supabase, media is LiveKit — the two are independent
  truths and **MUST NOT** be conflated. A LiveKit participant is not proof of event
  attendance and vice versa.
- **Ephemerality is guaranteed by omission**: the backend never calls any LiveKit
  Egress, recording, or transcription API — no such code path may exist. Only
  `started_at` / `ended_at` metadata is persisted; after a stream ends, no video
  artifact exists in any store.
- The backend is **authoritative for attendance and publish rights**. A client
  cannot grant itself the camera: publish permission is baked into the signed
  token, and a revoked permission is enforced server-side.

### User-facing outcome

| | |
|---|---|
| **Who needs it** | A person joining an online event stream. |
| **Problem solved** | People attending the same online event can see and talk to each other. |
| **Success** | The user joins the online event, the roster **indicates they are live**, and when they leave — or silently die — they are **automatically disconnected** from the room. |

### SFU choice — why LiveKit

Audio spaces proxy Cloudflare Calls. Video is 10–50× the bitrate of audio, so a
per-minute-metered SFU is the wrong cost shape here. LiveKit is chosen because it is
the closest open-source analogue to the existing pattern:

| Criterion | LiveKit |
|---|---|
| License | Apache-2.0, self-hostable |
| Cost shape | Fixed VM cost, not per-participant-minute |
| Local dev | `livekit/livekit-server --dev` in Docker — no cloud account, no secrets to obtain |
| .NET integration | Plain HTTPS + HS256 JWT. **No proprietary SDK dependency is added** — `System.IdentityModel.Tokens.Jwt` is already referenced by `AlSaqr.Infrastructure` (`TokenService.cs`) |
| Cost controls | Simulcast, dynacast, `emptyTimeout`, `maxParticipants`, per-source publish grants |

Alternatives considered and rejected: **mediasoup** / **Janus** (no server REST
control plane — would require a Node/C sidecar, breaking the "control plane in .NET"
shape), **Jitsi** (an application, not a library; its auth model does not map onto
`event_attendees`).

---

## Cost Model (binding)

"Cost effective" is a requirement, not an aspiration. The following are **MUST**
rules, each with the mechanism that enforces it.

| # | Rule | Mechanism |
|---|---|---|
| C1 | Video is available for **online events only**. | `is_online = false` → `ValidationException` (400) in the repository before any SFU call. |
| C2 | A room is **created on demand**, never pre-provisioned. | The room is created inside `StartEventStream` only. |
| C3 | An empty room **MUST** self-destruct. | `emptyTimeout = 120s` on `CreateRoom`, plus the reaper (step 7) as the authoritative backstop. |
| C4 | Only **presenters** may consume upstream bandwidth. | `canPublish` is false in a viewer's token; viewers are receive-only. |
| C5 | Concurrent publishers **MUST** be capped. | `MaxPresenters` (default **9**) enforced in the repository on promote; the host counts toward it. |
| C6 | Clients **MUST** publish simulcast with dynacast enabled. | Client-side `RoomOptions`; documented in step 10. Dynacast stops upstream layers nobody subscribes to. |
| C7 | Publish sources **MUST** be enumerated, not blanket. | `canPublishSources: ["camera","microphone"]` — screen share is out of scope, so its bitrate can never be incurred. |
| C8 | Join tokens **MUST** be short-lived. | TTL **10 minutes**; the token authorizes *entry*, and the room does not need to outlive it. |
| C9 | Silently-dead participants **MUST** be evicted. | Reaper closes them at **60s** of `last_seen_at` staleness, so the SFU stops relaying video nobody watches. |
| C10 | No recording, egress, or transcription. | No such call site may exist (see *Ephemerality*). This is also the single largest cost line in any video product. |

---

## Implementation Steps

### 1. Configuration (`AlSaqr.API/Config/AppSecrets.cs` + AWS secrets)

Add `LiveKitSettings { ApiKey, ApiSecret, HttpUrl, WsUrl }` to `AppSecrets`, bound to
a new `AlSaqr.Infrastructure/Config/LiveKitConfig.cs`:

```csharp
namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// LiveKit (self-hosted, Apache-2.0 SFU) settings, bound from the "LiveKit"
    /// configuration section. The API secret is server-side only: it signs join
    /// tokens and authenticates the room-admin API, and must never appear in a
    /// response, log line, or client-visible error
    /// (specs/video-streaming-online-events.md).
    /// </summary>
    public sealed class LiveKitConfig
    {
        public string ApiKey { get; set; } = default!;
        public string ApiSecret { get; set; } = default!;

        /// <summary>Server API base, e.g. http://localhost:7880</summary>
        public string HttpUrl { get; set; } = "http://localhost:7880";

        /// <summary>Client signalling URL handed to the browser, e.g. ws://localhost:7880</summary>
        public string WsUrl { get; set; } = "ws://localhost:7880";
    }
}
```

`WsUrl` is the **only** LiveKit value that may reach the browser. `ApiSecret` flows
through the existing AWS Secrets Manager → `AddInMemoryCollection` path in
`Program.cs` and **MUST NOT** appear in any response body, log line, or
client-visible error.

### 2. Tables + entities (`AlSaqr.Data/Entities/Meetup`)

Follow the existing entity conventions (`BaseModel`, `[Table]` / `[PrimaryKey]` /
`[Column]`, snake_case plural table names). DDL goes in
`AlSaqr.Data/Entities/Meetup/sql/video_streams.sql`.

**`video_streams` → `VideoStream` entity** (one row per streaming session)

| Column | C# | Notes |
|---|---|---|
| `id` | `Guid Id` | PK |
| `event_id` | `Guid EventId` | FK → `events(id)` |
| `room_name` | `string RoomName` | LiveKit room key, `event-{eventId}` |
| `host_id` | `Guid HostId` | FK → `users(id)`; the organizer who started it |
| `started_at` | `DateTime StartedAt` | |
| `ended_at` | `DateTime? EndedAt` | null while live; `IsLive == EndedAt is null` |

**`video_participants` → `VideoParticipant` entity**

| Column | C# | Notes |
|---|---|---|
| `id` | `Guid Id` | PK |
| `event_id` | `Guid EventId` | FK → `events(id)` (per requirement; denormalized for direct per-event queries) |
| `participant_id` | `Guid ParticipantId` | FK → `users(id)` — see the design note below |
| `video_stream_id` | `Guid VideoStreamId` | FK → `video_streams(id)` |
| `role` | `string Role` | `host` \| `presenter` \| `viewer` |
| `camera_enabled` | `bool CameraEnabled` | presenter's camera state (cost signal, not a permission) |
| `muted` | `bool Muted` | |
| `sfu_identity` | `string? SfuIdentity` | LiveKit participant identity; set while connected |
| `joined_at` | `DateTime JoinedAt` | |
| `left_at` | `DateTime? LeftAt` | null while present — **this is the "he's in the live" indicator** |
| `last_seen_at` | `DateTime LastSeenAt` | refreshed by any authenticated call from the participant; drives the reaper (step 7) |

> **Design note — `participant_id` references `users(id)`, not `attendees(id)`.**
> Meetup attendance is keyed on `attendees` (`event_attendees.attendee_id`), but the
> authenticated caller resolves to a **user id** (`IUserCacheService.GetLoggedInUser()`),
> and the reference implementation `space_participants.user_id` is user-scoped.
> Keying the roster on users keeps the join path free of an extra indirection and
> lets `BuildParticipantDtos` fetch usernames/avatars in one `users` query.
> Attendance is still verified through `attendees` → `event_attendees` on every join
> (see `IsEventAttendee`). If the roster must instead be attendee-scoped, only
> `IsEventAttendee` and the FK change — the rest of the design is unaffected.

The one-live-stream-per-event invariant **MUST** be enforced in the database
(partial unique index where `ended_at IS NULL`), not only in application code.

```sql
-- AlSaqr.Data/Entities/Meetup/sql/video_streams.sql
-- Live video rooms for ONLINE events (specs/video-streaming-online-events.md).
-- Only started_at/ended_at metadata is persisted; no video artifact is ever stored.

create table if not exists "alsaqr-2026".video_streams (
  id uuid not null default gen_random_uuid (),
  event_id uuid not null,
  room_name character varying not null,
  host_id uuid not null,
  started_at timestamp with time zone not null default now(),
  ended_at timestamp with time zone null,
  constraint video_streams_pkey primary key (id),
  constraint video_streams_event_id_fkey foreign KEY (event_id)
    references "alsaqr-2026".events (id) on update CASCADE on delete CASCADE,
  constraint video_streams_host_id_fkey foreign KEY (host_id)
    references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE
) TABLESPACE pg_default;

-- One live stream per event (C2 / 409 on a second start), enforced in the DB.
create unique INDEX IF not exists video_streams_one_live_per_event
  on "alsaqr-2026".video_streams using btree (event_id)
  where (ended_at is null) TABLESPACE pg_default;

create index IF not exists video_streams_live_lookup
  on "alsaqr-2026".video_streams using btree (event_id)
  where (ended_at is null) TABLESPACE pg_default;

create table if not exists "alsaqr-2026".video_participants (
  id uuid not null default gen_random_uuid (),
  event_id uuid not null,
  participant_id uuid not null,
  video_stream_id uuid not null,
  role character varying not null default 'viewer'::character varying,
  camera_enabled boolean not null default false,
  muted boolean not null default true,
  sfu_identity character varying null,
  joined_at timestamp with time zone not null default now(),
  left_at timestamp with time zone null,
  last_seen_at timestamp with time zone not null default now(),
  constraint video_participants_pkey primary key (id),
  constraint video_participants_event_id_fkey foreign KEY (event_id)
    references "alsaqr-2026".events (id) on update CASCADE on delete CASCADE,
  constraint video_participants_participant_id_fkey foreign KEY (participant_id)
    references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE,
  constraint video_participants_video_stream_id_fkey foreign KEY (video_stream_id)
    references "alsaqr-2026".video_streams (id) on update CASCADE on delete CASCADE,
  constraint video_participants_role_check check (
    (role)::text = any (
      (array[
        'host'::character varying,
        'presenter'::character varying,
        'viewer'::character varying
      ])::text[]
    )
  )
) TABLESPACE pg_default;

-- One row per user per stream; rejoin updates the row rather than inserting.
create unique INDEX IF not exists video_participants_stream_user_key
  on "alsaqr-2026".video_participants using btree (video_stream_id, participant_id)
  TABLESPACE pg_default;

create index IF not exists video_participants_live
  on "alsaqr-2026".video_participants using btree (video_stream_id)
  where (left_at is null) TABLESPACE pg_default;

-- Reaper scan path (C9): stale live participants across all live streams.
create index IF not exists video_participants_last_seen
  on "alsaqr-2026".video_participants using btree (last_seen_at)
  where (left_at is null) TABLESPACE pg_default;
```

```csharp
// AlSaqr.Data/Entities/Meetup/VideoParticipant.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// A participant of an online event's video stream
    /// (specs/video-streaming-online-events.md). Role is backend-authoritative:
    /// publish rights are baked into the signed LiveKit token, so a client cannot
    /// grant itself a camera. LeftAt == null is the "in the live" indicator.
    /// </summary>
    [Table("video_participants")]
    public class VideoParticipant : BaseModel
    {
        public const string RoleHost = "host";
        public const string RolePresenter = "presenter";
        public const string RoleViewer = "viewer";

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("event_id")]
        public Guid EventId { get; set; }

        /// <summary>The joining user (see the design note in the spec).</summary>
        [Column("participant_id")]
        public Guid ParticipantId { get; set; }

        [Column("video_stream_id")]
        public Guid VideoStreamId { get; set; }

        /// <summary>"host" | "presenter" | "viewer"</summary>
        [Column("role")]
        public string Role { get; set; } = RoleViewer;

        [Column("camera_enabled")]
        public bool CameraEnabled { get; set; }

        [Column("muted")]
        public bool Muted { get; set; } = true;

        /// <summary>LiveKit participant identity — set while connected.</summary>
        [Column("sfu_identity")]
        public string? SfuIdentity { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the participant is in the live stream.</summary>
        [Column("left_at")]
        public DateTime? LeftAt { get; set; }

        /// <summary>
        /// Refreshed by any authenticated stream call from the participant; drives
        /// the reaper that disconnects silently-dead participants (C9).
        /// </summary>
        [Column("last_seen_at")]
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
```

```csharp
// AlSaqr.Data/Entities/Meetup/VideoStream.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.Meetup
{
    /// <summary>
    /// An ephemeral video stream for an online event
    /// (specs/video-streaming-online-events.md). Only started_at/ended_at metadata
    /// is ever persisted — no video artifact exists in any store.
    /// </summary>
    [Table("video_streams")]
    public class VideoStream : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("event_id")]
        public Guid EventId { get; set; }

        /// <summary>LiveKit room key: "event-{eventId}".</summary>
        [Column("room_name")]
        public string RoomName { get; set; } = string.Empty;

        [Column("host_id")]
        public Guid HostId { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the stream is live.</summary>
        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }

        public static string RoomNameFor(Guid eventId) => $"event-{eventId}";
    }
}
```

### 3. DTOs (`AlSaqr.Domain/Meetup/EventVideo.cs`)

Follow the existing pattern — a static class with nested DTOs, exactly as
`AlSaqr.Domain.SocialMedia.Spaces`. Explicit `[JsonPropertyName]` on every response
field; these shapes are the client contract:

```typescript
type VideoRole = 'host' | 'presenter' | 'viewer';

interface VideoStreamToDisplay {
  videoStreamId: string; eventId: string; eventName?: string;
  hostId: string; hostUsername?: string; hostAvatar?: string;
  startedAt: string; endedAt?: string;
  participantCount: number; isLive: boolean;
}

interface VideoParticipantDto {
  participantId: string; username: string; avatar?: string;
  role: VideoRole; cameraEnabled: boolean; muted: boolean;
}

// The connect payload: everything the browser needs to reach the SFU, and
// nothing more. `wsUrl` + `token` are the ONLY LiveKit values that leave the API.
interface JoinVideoStreamResultDto {
  stream: VideoStreamToDisplay; role: VideoRole;
  wsUrl: string; token: string; expiresAt: string;
  canPublish: boolean;
  participants: VideoParticipantDto[];
}
```

Request forms (each wrapped in the existing `AlSaqrUpsertRequest<T>` `{ values: ... }`
envelope): `StartVideoStreamForm { }` (empty today — reserved for a future title),
`CameraStateForm { CameraEnabled, Muted }`.

### 4. LiveKit control-plane service (`AlSaqr.Infrastructure/Video`)

`ILiveKitVideoService`, registered as a typed `HttpClient`
(`AddHttpClient<ILiveKitVideoService, LiveKitVideoService>`). This is the
video-side analogue of `ICloudflareCallsService` — a **new** file that does not
touch it.

The service **MUST NOT** contain any call to LiveKit Egress, recording, or
transcription endpoints — those code paths must not exist.

```csharp
// AlSaqr.Infrastructure/Video/LiveKitVideoService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>A minted join token and the moment it stops being valid.</summary>
    public sealed record LiveKitJoinToken(string Token, string WsUrl, DateTime ExpiresAt);

    /// <summary>
    /// Control plane for the self-hosted LiveKit SFU
    /// (specs/video-streaming-online-events.md). This is the ONLY place the API
    /// secret is used: it signs room-scoped join tokens and authenticates the
    /// room-admin API. The browser receives a token and the ws URL — never the key
    /// or secret. Ephemerality is guaranteed by omission: this service exposes no
    /// egress, recording, or transcription call — such code paths must not be added.
    /// </summary>
    public interface ILiveKitVideoService
    {
        /// <summary>
        /// Creates the room with the cost guards of the spec (C3/C5): an empty room
        /// self-destructs and the participant count is capped. Idempotent — creating
        /// an existing room returns it unchanged.
        /// </summary>
        Task CreateRoomAsync(string roomName, int maxParticipants, CancellationToken ct = default);

        /// <summary>
        /// Mints a short-lived (C8), room-scoped token. <paramref name="canPublish"/>
        /// is the whole permission model: a viewer's token cannot publish, so a
        /// client that flips its own role locally still cannot send media.
        /// </summary>
        LiveKitJoinToken MintJoinToken(
            string roomName, string identity, string displayName, bool canPublish);

        /// <summary>
        /// Force-disconnects one participant. Used on leave, demote, end, and reap.
        /// Best-effort: a participant or room that is already gone is not an error.
        /// </summary>
        Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct = default);

        /// <summary>
        /// Deletes the room, disconnecting everyone. Used on host-end and by the
        /// reaper. Best-effort, for the same reason as RemoveParticipantAsync.
        /// </summary>
        Task DeleteRoomAsync(string roomName, CancellationToken ct = default);
    }

    public sealed class LiveKitVideoService : ILiveKitVideoService
    {
        // C8: the token authorizes ENTRY; the session outlives it via the SFU's own
        // connection state, so a long TTL buys nothing and widens the replay window.
        private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(10);

        // C3: an empty room self-destructs without waiting for the reaper tick.
        private const int EmptyTimeoutSeconds = 120;

        private readonly HttpClient _httpClient;
        private readonly LiveKitConfig _config;

        public LiveKitVideoService(HttpClient httpClient, IOptions<LiveKitConfig> config)
        {
            _httpClient = httpClient;
            _config = config.Value;
        }

        public Task CreateRoomAsync(string roomName, int maxParticipants, CancellationToken ct = default) =>
            PostAsync(
                "CreateRoom",
                new CreateRoomRequest
                {
                    Name = roomName,
                    EmptyTimeout = EmptyTimeoutSeconds,
                    MaxParticipants = maxParticipants,
                },
                adminRoom: roomName,
                ct);

        public LiveKitJoinToken MintJoinToken(
            string roomName, string identity, string displayName, bool canPublish)
        {
            var expiresAt = DateTime.UtcNow.Add(TokenTtl);

            // C4/C7: viewers get no publish grant at all, and presenters get an
            // enumerated source list — screen share is out of scope, so its
            // bitrate can never be incurred.
            var grant = new Dictionary<string, object>
            {
                ["room"] = roomName,
                ["roomJoin"] = true,
                ["canSubscribe"] = true,
                ["canPublish"] = canPublish,
                ["canPublishData"] = true,
                ["canPublishSources"] = canPublish
                    ? new[] { "camera", "microphone" }
                    : Array.Empty<string>(),
            };

            return new LiveKitJoinToken(
                WriteToken(identity, displayName, grant, expiresAt),
                _config.WsUrl,
                expiresAt);
        }

        public Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct = default) =>
            PostAsync(
                "RemoveParticipant",
                new RemoveParticipantRequest { Room = roomName, Identity = identity },
                adminRoom: roomName,
                ct);

        public Task DeleteRoomAsync(string roomName, CancellationToken ct = default) =>
            PostAsync(
                "DeleteRoom",
                new DeleteRoomRequest { Room = roomName },
                adminRoom: roomName,
                ct);

        // ----- internals -----

        /// <summary>
        /// LiveKit's server API is Twirp-over-HTTP: POST /twirp/{service}/{method}
        /// with a JSON body, authorized by a JWT carrying admin grants.
        /// </summary>
        private async Task PostAsync(string method, object body, string adminRoom, CancellationToken ct)
        {
            var adminGrant = new Dictionary<string, object>
            {
                ["roomCreate"] = true,
                ["roomAdmin"] = true,
                ["room"] = adminRoom,
            };

            var jwt = WriteToken(
                identity: "alsaqr-backend",
                displayName: "alsaqr-backend",
                grant: adminGrant,
                expiresAt: DateTime.UtcNow.AddMinutes(1));

            var url = $"{_config.HttpUrl.TrimEnd('/')}/twirp/livekit.RoomService/{method}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {jwt}");
            request.Content = JsonContent.Create(body, options: JsonOptions);

            var response = await _httpClient.SendAsync(request, ct);

            // Best-effort teardown: the room/participant may already be gone (peer
            // died and the SFU reaped it). Anything else is a real failure.
            if (!response.IsSuccessStatusCode
                && response.StatusCode != System.Net.HttpStatusCode.NotFound
                && response.StatusCode != System.Net.HttpStatusCode.Gone)
            {
                // Never echo the response body — it may carry configuration detail.
                throw new InvalidOperationException(
                    $"LiveKit {method} failed with status {(int)response.StatusCode}.");
            }
        }

        private string WriteToken(
            string identity, string displayName, IDictionary<string, object> grant, DateTime expiresAt)
        {
            var now = DateTimeOffset.UtcNow;
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.ApiSecret)),
                SecurityAlgorithms.HmacSha256);

            // Built as a raw JwtPayload rather than a ClaimsIdentity because
            // LiveKit expects "video" to be a nested JSON object, which the claim
            // pipeline would flatten to a string.
            var payload = new JwtPayload
            {
                { "iss", _config.ApiKey },
                { "sub", identity },
                { "name", displayName },
                { "nbf", now.ToUnixTimeSeconds() },
                { "exp", new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds() },
                { "video", grant },
            };

            return new JwtSecurityTokenHandler()
                .WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
        }

        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ----- LiveKit wire models (transport-only; not domain DTOs) -----

        private sealed class CreateRoomRequest
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            /// <summary>Seconds an empty room survives before the SFU closes it (C3).</summary>
            [JsonPropertyName("emptyTimeout")]
            public int EmptyTimeout { get; set; }

            [JsonPropertyName("maxParticipants")]
            public int MaxParticipants { get; set; }
        }

        private sealed class RemoveParticipantRequest
        {
            [JsonPropertyName("room")]
            public string Room { get; set; } = string.Empty;

            [JsonPropertyName("identity")]
            public string Identity { get; set; } = string.Empty;
        }

        private sealed class DeleteRoomRequest
        {
            [JsonPropertyName("room")]
            public string Room { get; set; } = string.Empty;
        }
    }
}
```

> **Note on `ApiSecret` length.** LiveKit requires a secret of at least 32
> characters in production. The `--dev` server accepts the well-known
> `devkey` / `secret` pair; production secrets come from AWS Secrets Manager.

### 5. Supabase realtime broadcaster (`AlSaqr.Infrastructure/Video`)

`IEventVideoBroadcaster` using the **service-role-secret** key to broadcast on
`event_video:{eventId}`:

- `participant_joined { participantId, role }` — after a successful join. **This is
  the "he's in the live" signal** other clients react to.
- `participant_left { participantId }` — after leave / removal / reap.
- `role_changed { participantId, role }` — after promote / demote.
- `stream_started { videoStreamId, eventId, startedAt }`
- `stream_ended { videoStreamId, endedAt }` — after host end or reaper end.

Camera/mic toggles are **client-emitted** via LiveKit's own track events; the backend
neither emits nor re-broadcasts them.

> **Known DRY deviation (CLAUDE.md §4).** The Supabase broadcast POST is already
> implemented in `SpaceEventBroadcaster`. Factoring it into a shared
> `SupabaseBroadcastClient` would require editing the frozen audio-spaces file, so
> this feature **MUST** add `AlSaqr.Infrastructure/Video/SupabaseBroadcastClient.cs`
> (channel-agnostic: `BroadcastAsync(topic, eventName, payload, ct)`) and build
> `EventVideoBroadcaster` on top of it. A follow-up **SHOULD** migrate
> `SpaceEventBroadcaster` onto the same client once the freeze lifts; that
> refactor is out of scope here and is recorded so the duplication is deliberate
> and temporary rather than accidental.

### 6. Repository (`AlSaqr.Data/Repositories/Meetup`)

`IVideoStreamRepository` + `VideoStreamRepository`, `Supabase.Client` passed per
method (§3.1). Custom exceptions are thrown **inside the repository** per §3.3.

```csharp
public interface IVideoStreamRepository
{
    /// <summary>Live stream for an online event, or null when none is live.</summary>
    Task<VideoStreamToDisplay?> GetLiveEventStream(
        Supabase.Client supabase, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Starts the stream; the caller becomes host. Throws Validation when the event
    /// is not online (C1), Forbidden when the caller is not the event organizer, and
    /// Conflict when the event already has a live stream.
    /// </summary>
    Task<VideoStream> StartEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Registers the caller on the live stream as host (organizer) or viewer, and
    /// returns the participant row plus the stream. Throws NotFound when no stream
    /// is live and Forbidden when the caller does not attend the event.
    /// </summary>
    Task<(VideoStream Stream, VideoParticipant Participant)> JoinEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Marks the caller as left and clears SFU state. Returns the participant as it
    /// was before leaving (so the SFU disconnect can be issued), or null when the
    /// caller was not live — leave is idempotent.
    /// </summary>
    Task<VideoParticipant?> LeaveEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Ends a live stream (host only — Forbidden otherwise). Stamps ended_at, marks
    /// every participant left, and returns the stream so its room can be deleted.
    /// </summary>
    Task<VideoStream> EndEventStream(
        Supabase.Client supabase, Guid hostUserId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// The caller's live participant row; throws NotFound for a dead stream and
    /// Forbidden when the caller is not live on it. Refreshes last_seen_at.
    /// </summary>
    Task<VideoParticipant> GetLiveParticipant(
        Supabase.Client supabase, Guid videoStreamId, Guid userId, CancellationToken ct);

    /// <summary>Records the LiveKit identity granted to a connected participant.</summary>
    Task SetSfuIdentity(
        Supabase.Client supabase, Guid videoStreamId, Guid userId, string sfuIdentity, CancellationToken ct);

    /// <summary>Persists the caller's camera/mute state (a cost signal, not a permission).</summary>
    Task SetCameraState(
        Supabase.Client supabase, Guid videoStreamId, Guid userId, bool cameraEnabled, bool muted, CancellationToken ct);

    /// <summary>
    /// Host promotes a viewer → presenter. Throws Forbidden unless the caller is the
    /// host, NotFound when the target is not live, and Conflict when the presenter
    /// cap (C5) is already reached.
    /// </summary>
    Task<VideoParticipant> PromotePresenter(
        Supabase.Client supabase, Guid hostUserId, Guid videoStreamId, Guid targetUserId, CancellationToken ct);

    /// <summary>Host demotes a presenter → viewer. Returns the pre-demotion snapshot.</summary>
    Task<VideoParticipant> DemotePresenter(
        Supabase.Client supabase, Guid hostUserId, Guid videoStreamId, Guid targetUserId, CancellationToken ct);

    /// <summary>Roster of a stream, deterministic order (joined_at asc, id asc).</summary>
    Task<List<VideoParticipantDto>> GetParticipants(
        Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

    // ----- Reaper support (step 7) -----

    Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct);

    Task<List<VideoParticipant>> GetLiveParticipants(
        Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

    Task MarkParticipantLeft(
        Supabase.Client supabase, VideoParticipant participant, CancellationToken ct);

    /// <summary>System end (reaper): stamps ended_at without a host check.</summary>
    Task<VideoStream> EndStreamSystem(
        Supabase.Client supabase, VideoStream stream, CancellationToken ct);
}
```

The two authorization gates, which are the heart of the feature:

```csharp
// ✓ C1 — video is for ONLINE events only; checked before any SFU call is made.
private static async Task<Event> GetOnlineEventOrThrow(
    Supabase.Client supabase, Guid eventId, CancellationToken ct)
{
    var existing = await supabase
        .From<Event>()
        .Filter("id", Operator.Equals, eventId.ToString())
        .Single(ct);

    if (existing == null)
        throw new NotFoundException($"Event {eventId} does not exist.");

    if (!existing.IsOnline)
        throw new ValidationException("Video streaming is available for online events only.");

    return existing;
}

// ✓ "Only users in the event can join the video call." Attendance lives on
// event_attendees, keyed on attendees.id — so the user is resolved to their
// attendee row first. No attendee row means the user attends nothing at all.
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
```

### 7. Reaper (`AlSaqr.API/HostedServices/VideoStreamReaperService.cs`)

A `BackgroundService` registered like `SpaceReaperService` (a **new** file — the
space reaper is not modified) that scans every ~30s:

- A participant with `left_at IS NULL` whose `last_seen_at` is older than the
  timeout (default **60s**) is reaped: `RemoveParticipantAsync` on LiveKit, stamp
  `left_at`, broadcast `participant_left`. **This is the "automatically disconnects
  the user" guarantee of the success criteria.**
- A live stream with zero un-left participants past the timeout is ended: stamp
  `ended_at`, `DeleteRoomAsync`, broadcast `stream_ended`.

LiveKit's own `emptyTimeout` (C3) closes the room server-side as a second line of
defence, but the database is authoritative and the reaper is what reconciles it.

> **Optional hardening (MAY).** LiveKit can POST `participant_left` /
> `room_finished` webhooks, which would make disconnects near-instant instead of
> bounded by the 30s tick. That is an additive endpoint and is **out of scope** for
> this spec; the reaper alone satisfies the requirement.

### 8. Controller (`AlSaqr.API/Controllers/Meetup/EventVideoController.cs`)

A **new** controller — `SpacesController.cs` is not touched.
`EventVideoController : AuthorizedControllerBase`, `[Route("[controller]")]` (the
`api` prefix comes from `UseRoutePrefix`), constructor-injected `Supabase.Client`,
`IVideoStreamRepository`, `ILiveKitVideoService`, `IEventVideoBroadcaster`,
`IUserCacheService`. Every action validates the token via `ValidateAccessToken()`
and resolves the caller from `IUserCacheService.GetLoggedInUser()` (never an ad-hoc
re-fetch, §4.1).

| Method | Route | Body | Returns |
|---|---|---|---|
| GET  | `api/EventVideo/event/{eventId}/live` | — | `VideoStreamToDisplay` or `null` |
| POST | `api/EventVideo/event/{eventId}/start` | `{ values: {} }` | `VideoStreamToDisplay` — organizer only |
| POST | `api/EventVideo/event/{eventId}/join` | `{}` | `JoinVideoStreamResultDto` |
| POST | `api/EventVideo/event/{eventId}/leave` | `{}` | 204 |
| POST | `api/EventVideo/event/{eventId}/end` | `{}` | 204 — host only |
| POST | `api/EventVideo/{videoStreamId}/heartbeat` | `{}` | 204 — refreshes `last_seen_at` |
| POST | `api/EventVideo/{videoStreamId}/camera` | `{ values: { cameraEnabled, muted } }` | 204 |
| POST | `api/EventVideo/{videoStreamId}/presenters/{userId}/promote` | `{}` | 204 — host only |
| POST | `api/EventVideo/{videoStreamId}/presenters/{userId}/demote` | `{}` | 204 — host only |

These endpoints are **not paginated** — plain JSON bodies, no `pagination` header,
no `currentPage` / `itemsPerPage` / `searchTerm` query params.

#### Reference example — connecting to the video stream

```csharp
/// <summary>
/// Joins the live video stream of an online event. Returns the stream, the
/// caller's role, and a short-lived room-scoped LiveKit token: the browser
/// connects with { wsUrl, token } and never sees the API secret. Only users
/// attending the event get past the repository's attendance gate — a
/// non-attendee is rejected with 403 before any SFU call is made.
/// </summary>
[HttpPost("event/{eventId}/join")]
public async Task<IActionResult> JoinEventStream(Guid eventId)
{
    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;

    var userId = GetLoggedInUserId();
    if (userId == Guid.Empty || eventId == Guid.Empty)
        return BadRequest("Missing required fields");

    var ct = HttpContext.RequestAborted;

    // Gate first: online event (C1) + attendance + live stream. Every 403/404
    // surfaces as a repository exception mapped by the global middleware, so no
    // SFU work happens for an unauthorized caller.
    var (stream, participant) = await _videoStreams.JoinEventStream(_supabase, userId, eventId, ct);

    // The identity is stream-scoped so a stale token from a previous stream of
    // the same event can never be replayed into the current one.
    var identity = $"{stream.Id}:{userId}";
    var canPublish = participant.Role != VideoParticipant.RoleViewer;   // C4

    var currentUser = _userCacheService.GetLoggedInUser();
    var token = _livekit.MintJoinToken(
        stream.RoomName, identity, currentUser?.Username ?? userId.ToString(), canPublish);

    await _videoStreams.SetSfuIdentity(_supabase, stream.Id, userId, identity, ct);

    // "Indicate he's in the live": the roster row (left_at == null) is the state,
    // this broadcast is the notification other attendees react to.
    await _broadcaster.ParticipantJoinedAsync(eventId, userId, participant.Role, ct);

    return Ok(new JoinVideoStreamResultDto
    {
        Stream = await _videoStreams.GetLiveEventStream(_supabase, eventId, ct) ?? new(),
        Role = participant.Role,
        WsUrl = token.WsUrl,
        Token = token.Token,
        ExpiresAt = token.ExpiresAt,
        CanPublish = canPublish,
        Participants = await _videoStreams.GetParticipants(_supabase, stream.Id, ct),
    });
}
```

#### Reference example — disconnecting from the video stream

```csharp
/// <summary>
/// Leaves the event's video stream: force-disconnects the caller on the SFU and
/// announces participant_left. Idempotent — every client exit path (unmount, tab
/// close, explicit leave) calls it, and the reaper converges on the same steps
/// for a client that dies without calling it at all.
/// </summary>
[HttpPost("event/{eventId}/leave")]
public async Task<IActionResult> LeaveEventStream(Guid eventId)
{
    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;

    var userId = GetLoggedInUserId();
    if (userId == Guid.Empty || eventId == Guid.Empty)
        return BadRequest("Missing required fields");

    var ct = HttpContext.RequestAborted;
    var participant = await _videoStreams.LeaveEventStream(_supabase, userId, eventId, ct);

    if (participant != null)
    {
        // Removing the participant server-side is what actually stops the media;
        // trusting the client to close its own connection would leave the SFU
        // relaying video nobody watches (C9).
        if (!string.IsNullOrEmpty(participant.SfuIdentity))
        {
            await _livekit.RemoveParticipantAsync(
                VideoStream.RoomNameFor(eventId), participant.SfuIdentity, ct);
        }

        await _broadcaster.ParticipantLeftAsync(eventId, userId, ct);
    }

    return NoContent();
}
```

Orchestration for the remaining actions (controller → repo/service; no Supabase or
LiveKit calls inline beyond the injected services):

- **Start** (organizer only): repository validates online + organizer + no live
  stream → `CreateRoomAsync(roomName, MaxParticipants)` → broadcast `stream_started`.
- **End** (host only): repository stamps `ended_at` and marks everyone left →
  `DeleteRoomAsync` → broadcast `stream_ended`.
- **Promote / demote** (host only): flip the persisted role → broadcast
  `role_changed`. Because publish rights live in the token, a demoted presenter is
  **also** removed from the room (`RemoveParticipantAsync`) so their next join mints
  a viewer token; the client reconnects automatically on `role_changed`.
- **Heartbeat**: refreshes `last_seen_at` so the reaper does not evict a healthy
  participant. The client **MUST** call it at an interval well under the 60s timeout
  (~20s).

### 9. DI registration (`Program.cs`)

```csharp
// Online-event video streaming (specs/video-streaming-online-events.md):
// repository, self-hosted LiveKit control plane, realtime broadcaster, and the
// silent-death reaper. The LiveKit API secret stays server-side only.
builder.Services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
builder.Services.Configure<LiveKitConfig>(builder.Configuration.GetSection("LiveKit"));
builder.Services.AddHttpClient<ILiveKitVideoService, LiveKitVideoService>();
builder.Services.AddHttpClient<IEventVideoBroadcaster, EventVideoBroadcaster>();
builder.Services.AddHostedService<VideoStreamReaperService>();
```

All against interfaces (§3.1). No code is added to `AlSaqr.Services` (§2).

### 10. Local development

The SFU runs locally in Docker; the API and the React client talk to it over
localhost. No cloud account and no paid service are involved.

```yaml
# docker-compose.livekit.yml — local dev only
services:
  livekit:
    image: livekit/livekit-server:latest
    command: --dev --bind 0.0.0.0
    ports:
      - "7880:7880"     # HTTP signalling + Twirp server API
      - "7881:7881"     # TCP fallback for WebRTC
      - "7882:7882/udp" # primary WebRTC media
```

`--dev` starts with the well-known key pair `devkey` / `secret`, so
`appsettings.Development.json` is:

```json
{
  "LiveKit": {
    "ApiKey": "devkey",
    "ApiSecret": "secret",
    "HttpUrl": "http://localhost:7880",
    "WsUrl": "ws://localhost:7880"
  }
}
```

The browser consumes the join response with the open-source `livekit-client` SDK.
C6 lives here — simulcast plus dynacast is what keeps upstream bandwidth
proportional to what is actually being watched:

```ts
// The token and wsUrl come from POST api/EventVideo/event/{eventId}/join
const room = new Room({
  adaptiveStream: true,   // downscale subscriptions to the rendered size
  dynacast: true,         // C6 — stop publishing layers nobody subscribes to
  publishDefaults: { simulcast: true },
});

await room.connect(wsUrl, token);
if (canPublish) await room.localParticipant.enableCameraAndMicrophone();

// Disconnect: call the API first so the backend is authoritative, then drop the
// peer connection. Calling only room.disconnect() would leave the roster stale
// until the reaper runs.
await api.post(`api/EventVideo/event/${eventId}/leave`, {});
await room.disconnect();
```

### 11. Integration tests

Per §Validation (Testcontainers or a dedicated test schema with per-test rollback;
never the shared dev DB). LiveKit is faked behind `ILiveKitVideoService`; broadcasts
are asserted through a fake `IEventVideoBroadcaster`. The authorization matrix below
is the primary test surface, and the token grant is asserted by decoding the minted
JWT — a viewer token whose `video.canPublish` is true is a security defect, not a
cosmetic one.

---

## Rules

**Authorization (backend-authoritative — the client never decides who may publish)**

- Video streaming exists **only** for events with `is_online = true`. A stream
  request for an in-person event is **400** (`ValidationException`), rejected before
  any SFU call (C1).
- **Only users attending the event may join** — attendance is verified through
  `attendees` → `event_attendees` on **every** join, including while the stream is
  live, so a non-attendee can never enter mid-stream. Non-attendees get **403**.
- Only the event **organizer** may start a stream; only the **host** (the organizer
  who started it) may end it or promote/demote presenters. Anyone else: **403**.
- **One live stream per event**; starting a second is **409** (`ConflictException`),
  enforced by the partial unique index.
- Publish rights are carried by the **signed token**: a viewer's token has
  `canPublish: false`, so a client that flips its own role locally still cannot send
  media. Role changes take effect only after the backend mints a new token.
- The presenter cap (C5) is enforced in the repository; exceeding it is **409**.
- Every endpoint requires a valid Supabase JWT (`ValidateAccessToken()`); the
  logged-in user comes from `UserCacheService`, never an ad-hoc re-fetch (§4.1).

**SFU control plane**

- The LiveKit **API key and secret MUST NOT touch the browser**. The only values
  that leave the API are `wsUrl` and a minted, room-scoped, short-lived token.
- Tokens **MUST** be scoped to exactly one room and one identity, and identity
  **MUST** be `{videoStreamId}:{userId}` so a token cannot be replayed into a later
  stream of the same event.
- Disconnects are **server-issued**. A client closing its own peer connection is a
  hint, not a teardown; the backend always issues `RemoveParticipantAsync` /
  `DeleteRoomAsync`.
- Every exit path converges on the same teardown: explicit leave, host end, demote,
  and the reaper all remove the participant on the SFU and broadcast the
  corresponding event. A silently dead participant (network death, tab close)
  **MUST** be reaped within the timeout.

**Presence & events (channel `event_video:{eventId}`)**

- Presence is Supabase, media is LiveKit — never conflate them; an SFU participant
  is not proof of event attendance and vice versa.
- Backend-emitted (service-role-secret key): `stream_started`, `stream_ended`,
  `participant_joined`, `participant_left`, `role_changed`.
- Client-emitted (backend neither emits nor validates): camera/mic track events,
  which LiveKit already surfaces to every subscriber.

**Ephemerality**

- Streams are never recorded. **No egress, capture, or transcription call may exist
  anywhere in the code** — ephemerality is guaranteed by omission, not by a flag.
  Only `started_at` / `ended_at` metadata is persisted; no video artifact may exist
  in any store after a stream ends. This is also cost rule C10.

**Constitution compliance (CLAUDE.md)**

- Controllers never touch the Supabase or LiveKit clients directly beyond injected
  services/repos (§3.2); `Supabase.Client` is passed into repository methods, never
  stored (§3.1); DI against interfaces only (§3.1).
- Write failures throw custom exceptions **inside the repository**; HTTP mapping
  lives in the global exception middleware only (§3.3). The existing mapping table
  already covers every exception this feature throws — **no middleware change is
  required**.

  > **Prerequisite (blocking).** `app.UseMiddleware<ExceptionHandlingMiddleware>()`
  > is currently **commented out** in `Program.cs` (line 189). Until it is
  > re-enabled, every repository exception surfaces as an unhandled **500** instead
  > of 400/403/404/409, which fails this spec's authorization matrix outright.
  > Re-enabling it is a one-line prerequisite of this feature, not part of its
  > design. Controllers here **MUST NOT** compensate with try/catch (§3.3).
- **Video-stream endpoints are exempt from §4.1 caching** (this document is the
  exemption): live-stream lookups, rosters, and every mutation are real-time state
  and MUST always be read live. Nothing here is cached, so nothing needs
  invalidation.
- `System.Text.Json` only (§4); every async DB/HTTP call awaited, no
  fire-and-forget (§4) — including broadcasts.
- All list-ish reads (participants of a stream) use deterministic ordering
  (`joined_at` ascending, tie-broken by `id`) (§3.2).
- No code is added to `AlSaqr.Services` (§2).

**Feature isolation (the frozen-audio-spaces constraint)**

- No file under the audio-spaces implementation may be modified. Shared *concepts*
  (per-method Supabase client, reaper, broadcaster, repository-thrown exceptions)
  are re-implemented for video; shared *code* is limited to the constitution's own
  utilities and the new `SupabaseBroadcastClient`.

---

## Acceptance

- **Contract**: every route in the table above exists with the exact method, path,
  body envelope, and response shape; DTOs serialize camelCase and round-trip against
  the TypeScript interfaces (`videoStreamId`, `participantId`, `isLive`,
  `cameraEnabled`). No pagination header.
- **Online-only (C1)**: start/join against an in-person event → **400**, and the
  LiveKit fake records **zero** calls.
- **Attendance matrix** (integration-tested, per §Validation isolation):
  - non-attendee join → **403**, and no token is minted;
  - attendee join of a live stream → **200** with `{ wsUrl, token, role, participants }`;
  - start by a non-organizer → **403**; by the organizer → the stream and its room;
  - second live stream for the same event → **409**;
  - end / promote / demote by a non-host → **403**; by the host → **204**;
  - promote past the presenter cap → **409**.
- **Token grant**: a viewer's minted token decodes to `video.canPublish == false`
  with an empty `canPublishSources`; a presenter's decodes to `true` with exactly
  `["camera","microphone"]`; both are scoped to the one room and expire within
  10 minutes.
- **"He's in the live"**: after join, the participant row has `left_at == null`, the
  roster returned by the live lookup includes them, and `participant_joined` is
  broadcast on `event_video:{eventId}`.
- **"Automatically disconnects"**: a participant whose `last_seen_at` exceeds 60s is
  reaped — `RemoveParticipantAsync` is called, `left_at` is stamped, and
  `participant_left` is broadcast — without any client action. A stream left empty
  past the timeout is auto-ended: `ended_at` stamped, room deleted, `stream_ended`
  broadcast, and the live lookup returns `null` afterward.
- **Leave teardown**: explicit leave removes the participant on the SFU and
  broadcasts `participant_left`; calling leave twice is a no-op, not an error.
- **Cost guards**: `CreateRoom` is called with `emptyTimeout = 120` and the
  configured `maxParticipants`; the client connects with `dynacast` and `simulcast`
  enabled.
- **Ephemerality**: `started_at` and `ended_at` are recorded; the codebase contains
  no egress/recording/transcription call site.
- **Secrets**: the LiveKit API key/secret and the Supabase service-role key come
  from configuration/AWS secrets and never appear in any response, log, or
  client-visible error.
- **Isolation**: `git diff` for this feature touches no audio-spaces file.
- Deterministic tests, runnable in CI, no shared dev DB (§Validation).

---

## Out of Scope

- Recording, transcription, or video persistence of any kind (C10).
- Video for **in-person** events, and any group-level (non-event) video.
- Screen sharing, virtual backgrounds, breakout rooms, chat overlay (event chat is
  the existing messaging feature), and reactions.
- Raise-hand → auto-promote flows; promotion is an explicit host action.
- LiveKit webhooks (an optional latency improvement over the reaper, noted in step 7).
- SFU scaling, cascading, region routing, and TURN provisioning beyond what a
  single self-hosted LiveKit node provides.
- Any change to audio spaces, including refactoring `SpaceEventBroadcaster` onto the
  new shared `SupabaseBroadcastClient` (recorded as a follow-up in step 5).
- Native mobile clients.
```

