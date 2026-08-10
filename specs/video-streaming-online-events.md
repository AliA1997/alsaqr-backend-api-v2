# Specification: Video Streaming for Online Events (Meetup)

> **Version 2** (2026-08-09) — supersedes v1. See *Changelog* at the end for the
> defects this revision closes.
>
> **Scope:** live video calls for **online events only** (`events.is_online = true`).
> Conformance keywords (**MUST**, **MUST NOT**, **SHOULD**, **MAY**) follow RFC 2119.
> Product context: [`video-streaming-online-events.prd.md`](./video-streaming-online-events.prd.md).
>
> **Constraint compliance (explicit).** The audio-spaces implementation is
> **frozen**: this feature **MUST NOT** edit `SpacesController.cs`, `Space`,
> `SpaceParticipant`, `SpaceRepository`, `CloudflareCallsService`, or
> `SpaceEventBroadcaster`, and **MUST NOT** add routes to the `Spaces` controller.
> Every artifact below is a *new* file. Audio spaces are the **reference pattern**,
> not a dependency — the two features share no code and no tables.

---

## 0. Blocking Prerequisites

This feature is an **authorization feature**: its entire value is "only users in the
event can join the video call." Three platform defects break that guarantee. None are
caused by this design, and none can be worked around inside it.

| ID | Defect | What it breaks | Required fix |
|---|---|---|---|
| **DEP-1** | `app.UseMiddleware<ExceptionHandlingMiddleware>()` is commented out ([`Program.cs:189`](../AlSaqr.API/Program.cs#L189)) | Every 400/403/404/409 in this spec returns **500**. The authorization matrix cannot be tested, let alone enforced | Uncomment. One line, middleware already written. Also silently affects audio spaces today |
| **DEP-2** | The access token's **signature is never verified** — [`Auth.cs:13`](../AlSaqr.Domain/Utils/Auth.cs#L13) decodes the payload and checks `exp` only, and [`access-token.md:53`](./access-token.md#L53) lists signature verification as out of scope | Any caller can mint a token with an arbitrary `sub` and a future `exp`. Identity is unauthenticated | Verify HS256 against the Supabase JWT secret before trusting any claim. See §1.2 |
| **DEP-3** | `UserCacheService` stores the logged-in user under the single literal key `"loggedInUser"` ([`UserCacheService.cs:57`](../AlSaqr.Infrastructure/UserCacheService.cs#L57)) in a process-wide singleton | Under concurrency `GetLoggedInUser()` returns whoever logged in most recently — **not the caller**. A join could mint user A's token for user B's request | Resolve the caller from the verified token (DEP-2); keep `UserCacheService` as a profile cache **keyed by user id** |

> **DEP-2 and DEP-3 compound.** Neither an unverified `sub` nor a global cache slot
> can identify a caller. Together they mean the backend cannot currently tell who is
> asking — and this feature's job is to sign a media credential for exactly one
> person. **This spec MUST NOT be implemented on top of the current identity path.**
> §1.2 defines the minimum correct resolver. Controllers here **MUST NOT** compensate
> with try/catch (CLAUDE.md §3.3).

---

## Overview

An online event gets one live, **ephemeral** video room:

- The event's **organizer** (or the host group's **founder** — see §6.1) starts the
  stream and becomes its **host**. Any user attending that event joins as a
  **viewer**; the host may promote a viewer to **presenter** (may turn on camera/mic)
  and demote them back.
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
  Egress, recording, or transcription API — no such code path may exist. No media is
  ever at rest, and the participation metadata that *is* persisted has a bounded
  retention (§9).
- The backend is **authoritative for attendance and publish rights**. A client cannot
  grant itself the camera: publish permission is baked into the signed token **and**
  revocable server-side mid-session (§4).

### User-facing outcome

| | |
|---|---|
| **Who needs it** | A person joining an online event stream. |
| **Problem solved** | People attending the same online event can see and talk to each other. |
| **Success** | The user joins the online event, the roster **indicates they are live**, and when they leave — or silently die — they are **automatically disconnected**, without a transient network blip costing them their seat or their role. |

### SFU choice — why LiveKit

Audio spaces proxy Cloudflare Calls. Video is 10–50× the bitrate of audio, so a
per-minute-metered SFU is the wrong cost shape here. LiveKit is chosen because it is
the closest open-source analogue to the existing pattern:

| Criterion | LiveKit |
|---|---|
| License | Apache-2.0, self-hostable |
| Cost shape | Fixed VM cost, not per-participant-minute |
| Local dev | `livekit/livekit-server --dev` in Docker — no cloud account, no secrets to obtain |
| .NET integration | Plain HTTPS + HS256 JWT. **No proprietary SDK dependency is added** — `System.IdentityModel.Tokens.Jwt` is already referenced by `AlSaqr.Infrastructure` ([`TokenService.cs:2`](../AlSaqr.Infrastructure/TokenService.cs#L2)) |
| Mid-session revocation | `UpdateParticipant` revokes publish rights server-side — required by §4 |

Alternatives considered and rejected: **mediasoup** / **Janus** (no server REST
control plane — would require a Node/C sidecar, breaking the "control plane in .NET"
shape), **Jitsi** (an application, not a library; its auth model does not map onto
`event_attendees`).

---

## Cost Model (binding)

"Cost effective" is a requirement, not an aspiration. Each rule is paired with the
mechanism that enforces it.

| # | Rule | Mechanism |
|---|---|---|
| C1 | Video is available for **online events only**. | `is_online = false` → `ValidationException` (400) in the repository before any SFU call. |
| C2 | A room is **created on demand**, never pre-provisioned. | The room is created inside `StartEventStream` only. |
| C3 | An empty room **MUST** self-destruct. | `emptyTimeout = 120s` on `CreateRoom`, plus the reaper (§7) as the authoritative backstop. |
| C4 | Only **presenters** may consume upstream bandwidth. | `canPublish` is false in a viewer's token; viewers are receive-only. |
| C5 | Concurrent publishers **MUST** be capped. | `LiveKitConfig.MaxPresenters` (default **9**) enforced in the repository on promote; the host counts toward it. |
| C6 | Clients **MUST** publish simulcast with dynacast enabled. | Client-side `RoomOptions`; documented in §11. Dynacast stops upstream layers nobody subscribes to. |
| C7 | Publish sources **MUST** be enumerated, not blanket. | `canPublishSources: ["camera","microphone"]` — screen share is out of scope, so its bitrate can never be incurred. |
| C8 | Join tokens **MUST** be short-lived. | TTL **90 seconds**. The token authorizes *entry*; the session outlives it via SFU connection state, so a long TTL buys nothing and only widens the replay window. |
| C9 | Silently-dead participants **MUST** be evicted. | Reaper closes them at **60s** of `last_seen_at` staleness, so the SFU stops relaying video nobody watches. |
| C10 | No recording, egress, or transcription. | No such call site may exist (§Rules). Also the largest cost line in any video product. |
| C11 | A stream **MUST NOT** run unbounded. | Hard ceiling of **4 hours** from `started_at`, enforced by the reaper regardless of activity. C3 and C9 cannot catch an *occupied but abandoned* room; this can. |
| C12 | Total room occupancy **MUST** be capped. | `LiveKitConfig.MaxParticipants` (default **50**) passed to `CreateRoom`. |

> **Cost scales with publishers × subscribers, not attendance.** At ~0.24 GB per
> subscriber-hour, 50 participants for one hour costs ~12 GB with one presenter and
> ~108 GB with nine. C5 is therefore the single most important cost lever in this
> document. See the PRD §5.2 for the full model.

---

## Implementation Steps

### 1. Configuration

#### 1.1 LiveKit settings

Add `LiveKitSettings { ApiKey, ApiSecret, HttpUrl, WsUrl, MaxParticipants, MaxPresenters }`
to `AppSecrets`, bound to a new `AlSaqr.Infrastructure/Config/LiveKitConfig.cs`:

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

        /// <summary>Total room occupancy cap (C12).</summary>
        public int MaxParticipants { get; set; } = 50;

        /// <summary>Concurrent publisher cap, host included (C5).</summary>
        public int MaxPresenters { get; set; } = 9;
    }
}
```

`WsUrl` is the **only** LiveKit value that may reach the browser. `ApiSecret` flows
through the existing AWS Secrets Manager → `AddInMemoryCollection` path in
`Program.cs` and **MUST NOT** appear in any response body, log line, or
client-visible error.

#### 1.2 Caller identity (closes DEP-2 + DEP-3)

Identity **MUST NOT** come from `IUserCacheService` in this feature. A new
`ICallerIdentityAccessor` in `AlSaqr.Infrastructure` resolves the caller from a
**signature-verified** token. This is additive — no existing file changes, and
`AuthorizedControllerBase.ValidateAccessToken()` keeps its current role as the
presence/expiry gate.

```csharp
// AlSaqr.Infrastructure/Auth/CallerIdentityAccessor.cs
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Infrastructure.Auth
{
    /// <summary>
    /// Resolves the acting user from the request's Bearer token, verifying the
    /// signature (specs/video-streaming-online-events.md §0 DEP-2/DEP-3).
    ///
    /// Why this exists: Auth.AccessTokenValidator deliberately does NOT verify the
    /// signature (specs/access-token.md, "Out of scope"), and UserCacheService keeps
    /// one global "loggedInUser" slot shared by every concurrent request. Neither
    /// can identify a caller. This feature signs a media credential for exactly one
    /// person, so it needs an identity it can actually trust.
    /// </summary>
    public interface ICallerIdentityAccessor
    {
        /// <summary>
        /// The verified caller id, or Guid.Empty when the token is absent, unsigned,
        /// tampered with, expired, or carries no usable subject claim.
        /// </summary>
        Guid GetCallerId();
    }

    public sealed class CallerIdentityAccessor : ICallerIdentityAccessor
    {
        private readonly IHttpContextAccessor _http;
        private readonly SupabaseAuthConfig _config;

        public CallerIdentityAccessor(IHttpContextAccessor http, IOptions<SupabaseAuthConfig> config)
        {
            _http = http;
            _config = config.Value;
        }

        public Guid GetCallerId()
        {
            var header = _http.HttpContext?.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(header))
                return Guid.Empty;

            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header["Bearer ".Length..].Trim()
                : header.Trim();

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.JwtSecret)),
                ValidateLifetime = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            try
            {
                new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);
                var sub = ((JwtSecurityToken)validated).Subject;
                return Guid.TryParse(sub, out var userId) ? userId : Guid.Empty;
            }
            catch (SecurityTokenException)
            {
                // A forged, tampered, or expired token is simply "no caller" — the
                // action then fails its own Guid.Empty check and returns 401/400.
                return Guid.Empty;
            }
        }
    }
}
```

Register `AddHttpContextAccessor()` and bind `SupabaseAuthConfig { JwtSecret }` from
the `Supabase` configuration section.

> **CLAUDE.md §4.1 note.** The constitution requires the logged-in *user profile* to
> be read via `UserCacheService`, never re-fetched ad hoc — that rule still applies
> here for display fields (username/avatar). What changes is that the **caller's
> identity** comes from the verified token, and the profile cache is consulted
> **keyed by that id**. §4.1 governs profile caching, not authentication.

### 2. Tables + entities (`AlSaqr.Data/Entities/Meetup`)

Follow the existing entity conventions (`BaseModel`, `[Table]` / `[PrimaryKey]` /
`[Column]`, snake_case plural table names). DDL goes in
`AlSaqr.Data/Entities/Meetup/sql/video_streams.sql`.

> **Documented deviation from CLAUDE.md §3.2.** §3.2 requires
> `[Table("name", Schema = "alsaqr-2026")]`. Entities here omit `Schema`, matching
> every existing entity in the solution (`Space`, `Event`, `EventAttendees`), because
> the schema is supplied once by `SupabaseOptions.Schema` in `Program.cs`. Adding
> `Schema` to these two entities alone would double the schema in the query path —
> the exact defect §3.2 forbids. This deviation is deliberate and consistent; it
> should be resolved solution-wide or not at all.

**`video_streams` → `VideoStream`** (one row per streaming session)

| Column | C# | Notes |
|---|---|---|
| `id` | `Guid Id` | PK |
| `event_id` | `Guid EventId` | FK → `events(id)` |
| `room_name` | `string RoomName` | LiveKit room key, **`stream-{id}`** — stream-scoped, never event-scoped (see §4) |
| `host_id` | `Guid HostId` | FK → `users(id)`; whoever started it |
| `started_at` | `DateTime StartedAt` | |
| `ended_at` | `DateTime? EndedAt` | null while live; `IsLive == EndedAt is null` |

**`video_participants` → `VideoParticipant`**

| Column | C# | Notes |
|---|---|---|
| `id` | `Guid Id` | PK |
| `event_id` | `Guid EventId` | FK → `events(id)` (per requirement; kept consistent with the stream by composite FK) |
| `participant_id` | `Guid ParticipantId` | FK → `users(id)` — see the design note below |
| `video_stream_id` | `Guid VideoStreamId` | FK → `video_streams(id)` |
| `role` | `string Role` | `host` \| `presenter` \| `viewer` |
| `camera_enabled` | `bool CameraEnabled` | presenter's camera state (cost signal, not a permission) |
| `muted` | `bool Muted` | |
| `sfu_identity` | `string? SfuIdentity` | LiveKit participant identity; set while connected |
| `joined_at` | `DateTime JoinedAt` | |
| `left_at` | `DateTime? LeftAt` | null while present — **the "he's in the live" indicator** |
| `left_reason` | `string? LeftReason` | `explicit` \| `reaped` \| `ended` \| `demoted` \| `expired` — makes the involuntary-disconnect guardrail measurable in SQL (§10) |
| `last_seen_at` | `DateTime LastSeenAt` | refreshed by any authenticated call from the participant; drives the reaper (§7) |

> **Design note — `participant_id` references `users(id)`, not `attendees(id)`.**
> Meetup attendance is keyed on `attendees` (`event_attendees.attendee_id`), but the
> caller resolves to a **user id** (§1.2), and the reference implementation
> `space_participants.user_id` is user-scoped. Keying the roster on users keeps the
> join path free of an extra indirection and lets the roster query fetch
> usernames/avatars in one `users` lookup. Attendance is still verified through
> `attendees` → `event_attendees` on every join (§6.1). If the roster must instead be
> attendee-scoped, only `IsEventAttendee` and the FK change.

```sql
-- AlSaqr.Data/Entities/Meetup/sql/video_streams.sql
-- Live video rooms for ONLINE events (specs/video-streaming-online-events.md).
-- No media is ever stored; participation metadata is retained 90 days (§9).

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
    references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE,
  -- Room names are stream-scoped, so they are globally unique by construction.
  -- This is what makes a token from an earlier stream unusable on a later one (§4).
  constraint video_streams_room_name_key unique (room_name),
  -- Target for the composite FK below: keeps video_participants.event_id honest.
  constraint video_streams_id_event_key unique (id, event_id)
) TABLESPACE pg_default;

-- One live stream per event (409 on a second start), enforced in the DB.
create unique INDEX IF not exists video_streams_one_live_per_event
  on "alsaqr-2026".video_streams using btree (event_id)
  where (ended_at is null) TABLESPACE pg_default;

-- Reaper scan path: all live streams, for the C11 duration ceiling.
create index IF not exists video_streams_live_lookup
  on "alsaqr-2026".video_streams using btree (started_at)
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
  left_reason character varying null,
  last_seen_at timestamp with time zone not null default now(),
  constraint video_participants_pkey primary key (id),
  constraint video_participants_participant_id_fkey foreign KEY (participant_id)
    references "alsaqr-2026".users (id) on update CASCADE on delete CASCADE,
  -- Composite FK: the denormalized event_id MUST match the stream's event_id.
  -- Without this the two columns can silently disagree.
  constraint video_participants_stream_event_fkey foreign KEY (video_stream_id, event_id)
    references "alsaqr-2026".video_streams (id, event_id) on update CASCADE on delete CASCADE,
  constraint video_participants_role_check check (
    (role)::text = any (
      (array[
        'host'::character varying,
        'presenter'::character varying,
        'viewer'::character varying
      ])::text[]
    )
  ),
  constraint video_participants_left_reason_check check (
    left_reason is null or (left_reason)::text = any (
      (array[
        'explicit'::character varying,
        'reaped'::character varying,
        'ended'::character varying,
        'demoted'::character varying,
        'expired'::character varying
      ])::text[]
    )
  ),
  -- left_at and left_reason are set together or not at all.
  constraint video_participants_left_consistency check (
    (left_at is null and left_reason is null)
    or (left_at is not null and left_reason is not null)
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

-- Retention purge path (§9).
create index IF not exists video_participants_left_at
  on "alsaqr-2026".video_participants using btree (left_at)
  where (left_at is not null) TABLESPACE pg_default;
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

        /// <summary>
        /// LiveKit room key. STREAM-scoped ("stream-{id}"), never event-scoped: a
        /// reused room name would let a token minted for an earlier stream of the
        /// same event be replayed into a later one (§4). Always read this column —
        /// never recompute the name at a call site.
        /// </summary>
        [Column("room_name")]
        public string RoomName { get; set; } = string.Empty;

        [Column("host_id")]
        public Guid HostId { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null while the stream is live.</summary>
        [Column("ended_at")]
        public DateTime? EndedAt { get; set; }

        /// <summary>The room name for a stream id. Used ONLY when creating a stream.</summary>
        public static string RoomNameFor(Guid videoStreamId) => $"stream-{videoStreamId}";
    }
}
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
    /// publish rights are baked into the signed token AND revocable mid-session via
    /// UpdateParticipant, so a client can neither grant nor keep a camera on its
    /// own. LeftAt == null is the "in the live" indicator.
    /// </summary>
    [Table("video_participants")]
    public class VideoParticipant : BaseModel
    {
        public const string RoleHost = "host";
        public const string RolePresenter = "presenter";
        public const string RoleViewer = "viewer";

        public const string LeftExplicit = "explicit";
        public const string LeftReaped = "reaped";
        public const string LeftEnded = "ended";
        public const string LeftDemoted = "demoted";
        public const string LeftExpired = "expired";

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
        /// Why the participant left. Set together with LeftAt. Distinguishing
        /// "reaped" from "explicit" is what makes the involuntary-disconnect
        /// guardrail a SQL query instead of a guess (§10).
        /// </summary>
        [Column("left_reason")]
        public string? LeftReason { get; set; }

        /// <summary>
        /// Refreshed by any authenticated stream call from the participant; drives
        /// the reaper that disconnects silently-dead participants (C9).
        /// </summary>
        [Column("last_seen_at")]
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
```

### 3. DTOs (`AlSaqr.Domain/Meetup/EventVideo.cs`)

A static class with nested DTOs, exactly as `AlSaqr.Domain.SocialMedia.Spaces`.
Explicit `[JsonPropertyName]` on every response field — these shapes are the client
contract.

```csharp
using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// DTOs for online-event video streaming
    /// (specs/video-streaming-online-events.md). Field names are a contract with
    /// the client and must serialize to exactly the documented TypeScript shapes.
    /// </summary>
    public static class EventVideo
    {
        // ----- Response DTOs -----

        public class VideoStreamToDisplay
        {
            [JsonPropertyName("videoStreamId")]
            public Guid VideoStreamId { get; set; }

            [JsonPropertyName("eventId")]
            public Guid EventId { get; set; }

            [JsonPropertyName("eventName")]
            public string? EventName { get; set; }

            [JsonPropertyName("hostId")]
            public Guid HostId { get; set; }

            [JsonPropertyName("hostUsername")]
            public string? HostUsername { get; set; }

            [JsonPropertyName("hostAvatar")]
            public string? HostAvatar { get; set; }

            [JsonPropertyName("startedAt")]
            public DateTime StartedAt { get; set; }

            [JsonPropertyName("endedAt")]
            public DateTime? EndedAt { get; set; }

            [JsonPropertyName("participantCount")]
            public int ParticipantCount { get; set; }

            [JsonPropertyName("isLive")]
            public bool IsLive { get; set; }
        }

        public class VideoParticipantDto
        {
            [JsonPropertyName("participantId")]
            public Guid ParticipantId { get; set; }

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }

            /// <summary>"host" | "presenter" | "viewer"</summary>
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("cameraEnabled")]
            public bool CameraEnabled { get; set; }

            [JsonPropertyName("muted")]
            public bool Muted { get; set; }
        }

        /// <summary>
        /// The connect payload: everything the browser needs to reach the SFU, and
        /// nothing more. wsUrl + token are the ONLY LiveKit values that leave the API.
        /// </summary>
        public class JoinVideoStreamResultDto
        {
            [JsonPropertyName("stream")]
            public VideoStreamToDisplay Stream { get; set; } = new();

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("wsUrl")]
            public string WsUrl { get; set; } = string.Empty;

            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("expiresAt")]
            public DateTime ExpiresAt { get; set; }

            [JsonPropertyName("canPublish")]
            public bool CanPublish { get; set; }

            [JsonPropertyName("participants")]
            public List<VideoParticipantDto> Participants { get; set; } = new();
        }

        // ----- Request forms (wrapped in AlSaqrUpsertRequest<T> { values: ... }) -----

        /// <summary>Reserved for a future stream title; empty today.</summary>
        public class StartVideoStreamForm { }

        public class CameraStateForm
        {
            public bool CameraEnabled { get; set; }
            public bool Muted { get; set; }
        }
    }
}
```

```typescript
// Serialized shapes (camelCase, System.Text.Json defaults + explicit names)
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

interface JoinVideoStreamResultDto {
  stream: VideoStreamToDisplay; role: VideoRole;
  wsUrl: string; token: string; expiresAt: string;
  canPublish: boolean; participants: VideoParticipantDto[];
}
```

### 4. LiveKit control-plane service (`AlSaqr.Infrastructure/Video`)

`ILiveKitVideoService`, registered as a typed `HttpClient`. This is the video-side
analogue of `ICloudflareCallsService` — a **new** file that does not touch it.

The service **MUST NOT** contain any call to LiveKit Egress, recording, or
transcription endpoints — those code paths must not exist.

**Two rules make the permission model real, and both are load-bearing:**

1. **Room names are stream-scoped** (`stream-{videoStreamId}`, globally unique by DB
   constraint). A token's grant is scoped to a room name, so a stream-scoped name
   means a token minted for an earlier stream **cannot** be replayed into a later one.
2. **Demotion revokes server-side via `UpdateParticipant`.** `RemoveParticipant` does
   not invalidate an already-issued token — a demoted presenter holding a valid
   `canPublish: true` token could otherwise reconnect and keep the floor.
   `UpdateParticipant` changes the permission on the SFU itself, effective
   immediately and independent of what the client holds.

```csharp
// AlSaqr.Infrastructure/Video/LiveKitVideoService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
        /// Creates the room with the cost guards of the spec (C3/C12): an empty room
        /// self-destructs and occupancy is capped. Idempotent — creating an existing
        /// room returns it unchanged.
        /// </summary>
        Task CreateRoomAsync(string roomName, int maxParticipants, CancellationToken ct = default);

        /// <summary>
        /// Mints a short-lived (C8), room-scoped token. <paramref name="canPublish"/>
        /// is half the permission model: a viewer's token cannot publish. The other
        /// half is <see cref="UpdatePublishPermissionAsync"/>, which revokes a
        /// permission already granted to a live session.
        /// </summary>
        LiveKitJoinToken MintJoinToken(
            string roomName, string identity, string displayName, bool canPublish);

        /// <summary>
        /// Changes a connected participant's publish rights server-side, effective
        /// immediately regardless of the token they hold. Required by demote: an
        /// issued token cannot be recalled, only overridden here.
        /// </summary>
        Task UpdatePublishPermissionAsync(
            string roomName, string identity, bool canPublish, CancellationToken ct = default);

        /// <summary>
        /// Force-disconnects one participant. Used on leave, end, and reap.
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
        private static readonly TimeSpan TokenTtl = TimeSpan.FromSeconds(90);

        // C3: an empty room self-destructs without waiting for the reaper tick.
        private const int EmptyTimeoutSeconds = 120;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

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

            return new LiveKitJoinToken(
                WriteToken(identity, displayName, PublishGrant(roomName, canPublish), expiresAt),
                _config.WsUrl,
                expiresAt);
        }

        public Task UpdatePublishPermissionAsync(
            string roomName, string identity, bool canPublish, CancellationToken ct = default) =>
            PostAsync(
                "UpdateParticipant",
                new UpdateParticipantRequest
                {
                    Room = roomName,
                    Identity = identity,
                    Permission = new ParticipantPermission
                    {
                        CanSubscribe = true,
                        CanPublish = canPublish,
                        CanPublishData = true,
                    },
                },
                adminRoom: roomName,
                ct);

        public Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct = default) =>
            PostAsync(
                "RemoveParticipant",
                new RemoveParticipantRequest { Room = roomName, Identity = identity },
                adminRoom: roomName,
                ct);

        public Task DeleteRoomAsync(string roomName, CancellationToken ct = default) =>
            PostAsync("DeleteRoom", new DeleteRoomRequest { Room = roomName }, adminRoom: roomName, ct);

        // ----- internals -----

        // C4/C7: viewers get no publish grant at all, and presenters get an
        // enumerated source list — screen share is out of scope, so its bitrate can
        // never be incurred.
        private static Dictionary<string, object> PublishGrant(string roomName, bool canPublish) => new()
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

            // Built as a raw JwtPayload rather than a ClaimsIdentity because LiveKit
            // expects "video" to be a nested JSON object, which the claim pipeline
            // would flatten to a string.
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

        private sealed class UpdateParticipantRequest
        {
            [JsonPropertyName("room")]
            public string Room { get; set; } = string.Empty;

            [JsonPropertyName("identity")]
            public string Identity { get; set; } = string.Empty;

            [JsonPropertyName("permission")]
            public ParticipantPermission Permission { get; set; } = new();
        }

        private sealed class ParticipantPermission
        {
            [JsonPropertyName("canSubscribe")]
            public bool CanSubscribe { get; set; }

            [JsonPropertyName("canPublish")]
            public bool CanPublish { get; set; }

            [JsonPropertyName("canPublishData")]
            public bool CanPublishData { get; set; }
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

```csharp
// AlSaqr.Infrastructure/Video/EventVideoBroadcaster.cs
namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// Publishes the backend-authoritative events of
    /// specs/video-streaming-online-events.md on the Supabase realtime channel
    /// <c>event_video:{eventId}</c> using the service-role key. Camera/mic toggles
    /// are client-emitted through LiveKit's own track events and are NOT re-emitted
    /// here. Presence is Supabase, media is LiveKit — this service only ever touches
    /// the Supabase side.
    /// </summary>
    public interface IEventVideoBroadcaster
    {
        /// <summary>After a stream is started, so attendees can surface "live now".</summary>
        Task StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, CancellationToken ct = default);

        /// <summary>After host end, C11 expiry, or the reaper ending an empty stream.</summary>
        Task StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, CancellationToken ct = default);

        /// <summary>After a successful join — the "he's in the live" signal.</summary>
        Task ParticipantJoinedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default);

        /// <summary>After leave / removal / reap.</summary>
        Task ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, CancellationToken ct = default);

        /// <summary>After promote / demote; the client re-joins to obtain a new token.</summary>
        Task RoleChangedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default);
    }

    public sealed class EventVideoBroadcaster : IEventVideoBroadcaster
    {
        private readonly ISupabaseBroadcastClient _broadcast;

        public EventVideoBroadcaster(ISupabaseBroadcastClient broadcast) => _broadcast = broadcast;

        private static string Topic(Guid eventId) => $"event_video:{eventId}";

        public Task StreamStartedAsync(Guid eventId, Guid videoStreamId, DateTime startedAt, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "stream_started", new { videoStreamId, eventId, startedAt }, ct);

        public Task StreamEndedAsync(Guid eventId, Guid videoStreamId, DateTime endedAt, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "stream_ended", new { videoStreamId, endedAt }, ct);

        public Task ParticipantJoinedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "participant_joined", new { participantId, role }, ct);

        public Task ParticipantLeftAsync(Guid eventId, Guid participantId, string reason, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "participant_left", new { participantId, reason }, ct);

        public Task RoleChangedAsync(Guid eventId, Guid participantId, string role, CancellationToken ct = default) =>
            _broadcast.BroadcastAsync(Topic(eventId), "role_changed", new { participantId, role }, ct);
    }
}
```

`ISupabaseBroadcastClient` is a new, channel-agnostic
`BroadcastAsync(topic, eventName, payload, ct)` over the Supabase
`/realtime/v1/api/broadcast` endpoint using the service-role key — structurally the
same POST already inside `SpaceEventBroadcaster.BroadcastAsync`.

> **Known DRY deviation (CLAUDE.md §4).** Extracting that POST from
> `SpaceEventBroadcaster` would require editing a frozen file, so it is
> re-implemented once in `SupabaseBroadcastClient`. A follow-up **SHOULD** migrate
> `SpaceEventBroadcaster` onto the same client when the freeze lifts. Recorded so the
> duplication is deliberate and temporary rather than accidental.

### 6. Repository (`AlSaqr.Data/Repositories/Meetup`)

`IVideoStreamRepository` + `VideoStreamRepository`, `Supabase.Client` passed per
method (§3.1). Custom exceptions are thrown **inside the repository** (§3.3).

Every method is keyed on **`eventId`**, never `videoStreamId`: exactly one stream per
event is live, so the event id addresses it unambiguously and clients never have to
track two identifiers.

```csharp
public interface IVideoStreamRepository
{
    /// <summary>Live stream for an online event, or null when none is live.</summary>
    Task<VideoStreamToDisplay?> GetLiveEventStream(
        Supabase.Client supabase, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Starts the stream; the caller becomes host. Throws Validation when the event
    /// is not online (C1) or has no host group, Forbidden when the caller is neither
    /// event organizer nor group founder, Conflict when a stream is already live.
    /// </summary>
    Task<VideoStream> StartEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Registers the caller on the live stream and returns the stream plus their
    /// participant row. Throws NotFound when no stream is live and Forbidden when
    /// the caller does not attend the event. Honours the reconnect grace window.
    /// </summary>
    Task<(VideoStream Stream, VideoParticipant Participant)> JoinEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Marks the caller as left with the given reason and clears SFU state. Returns
    /// the pre-leave snapshot so the SFU disconnect can be issued, or null when the
    /// caller was not live — leave is idempotent.
    /// </summary>
    Task<VideoParticipant?> LeaveEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, string reason, CancellationToken ct);

    /// <summary>
    /// Ends a live stream (host or group founder only — Forbidden otherwise). Stamps
    /// ended_at, marks every participant left with reason 'ended', and returns the
    /// stream so its room can be deleted.
    /// </summary>
    Task<VideoStream> EndEventStream(
        Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct);

    /// <summary>
    /// The caller's live participant row; throws NotFound for a dead stream and
    /// Forbidden when the caller is not live on it. Refreshes last_seen_at.
    /// </summary>
    Task<VideoParticipant> GetLiveParticipant(
        Supabase.Client supabase, Guid eventId, Guid userId, CancellationToken ct);

    /// <summary>Records the LiveKit identity granted to a connected participant.</summary>
    Task SetSfuIdentity(
        Supabase.Client supabase, Guid eventId, Guid userId, string sfuIdentity, CancellationToken ct);

    /// <summary>Persists camera/mute state (a cost signal, not a permission).</summary>
    Task SetCameraState(
        Supabase.Client supabase, Guid eventId, Guid userId, bool cameraEnabled, bool muted, CancellationToken ct);

    /// <summary>
    /// Host promotes a viewer → presenter. Forbidden unless the caller is host or
    /// founder, NotFound when the target is not live, Conflict when the presenter
    /// cap (C5) is reached. Returns the updated participant.
    /// </summary>
    Task<VideoParticipant> PromotePresenter(
        Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct);

    /// <summary>Host demotes a presenter → viewer. Returns the pre-demotion snapshot.</summary>
    Task<VideoParticipant> DemotePresenter(
        Supabase.Client supabase, Guid callerId, Guid eventId, Guid targetUserId, CancellationToken ct);

    /// <summary>Roster of a stream, deterministic order (joined_at asc, id asc).</summary>
    Task<List<VideoParticipantDto>> GetParticipants(
        Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

    // ----- Reaper support (§7) -----

    Task<List<VideoStream>> GetLiveStreams(Supabase.Client supabase, CancellationToken ct);

    Task<List<VideoParticipant>> GetLiveParticipants(
        Supabase.Client supabase, Guid videoStreamId, CancellationToken ct);

    Task MarkParticipantLeft(
        Supabase.Client supabase, VideoParticipant participant, string reason, CancellationToken ct);

    /// <summary>System end (reaper): stamps ended_at without an authority check.</summary>
    Task<VideoStream> EndStreamSystem(
        Supabase.Client supabase, VideoStream stream, CancellationToken ct);

    // ----- Retention (§9) -----

    /// <summary>Deletes participation rows whose left_at is older than the cutoff.</summary>
    Task<int> PurgeParticipantsLeftBefore(
        Supabase.Client supabase, DateTime cutoff, CancellationToken ct);
}
```

#### 6.1 Authorization gates

```csharp
// ✓ C1 — video is for ONLINE events only; checked before any SFU call is made.
// A groupless event is refused here too: event_attendees.group_id is NOT NULL, so
// an event with no host group has no attendees and therefore no one to authorize.
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

    if (existing.GroupId is null)
        throw new ValidationException("This event has no host group and cannot host video.");

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

// ✓ Two authorities exist in this codebase and BOTH are legitimate:
//   - event_attendees.is_event_organizer — the person running this event;
//   - groups.founder_id — the owner of the host group, already the authority for
//     RemoveEventAttendee (EventAttendeeRepository.cs).
// Founder-first resolution gives the founder an escalation path over an absent or
// abusive organizer, without demoting the organizer's day-to-day control.
private static async Task<bool> CanControlEventVideo(
    Supabase.Client supabase, Event hostEvent, Guid userId, CancellationToken ct)
{
    var founded = await supabase
        .From<Groups>()
        .Filter("id", Operator.Equals, hostEvent.GroupId!.ToString())
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
```

#### 6.2 Start — the pre-check is not the guard

```csharp
/// <summary>
/// The pre-check below is an optimisation for the common case; the partial unique
/// index is the actual guard. Two organizers pressing Start simultaneously both
/// pass the pre-check, and PostgREST returns a 23505 unique violation for the
/// loser — which MUST surface as 409, not as an unhandled 500.
/// </summary>
public async Task<VideoStream> StartEventStream(
    Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
{
    var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);

    if (!await CanControlEventVideo(supabase, hostEvent, userId, ct))
        throw new ForbiddenException("Only the event organizer or group founder may start the video stream.");

    var live = await supabase
        .From<VideoStream>()
        .Filter("event_id", Operator.Equals, eventId.ToString())
        .Filter("ended_at", Operator.Is, "null")
        .Single(ct);

    if (live != null)
        throw new ConflictException("This event already has a live video stream.");

    var streamId = Guid.NewGuid();
    var stream = new VideoStream
    {
        Id = streamId,
        EventId = eventId,
        RoomName = VideoStream.RoomNameFor(streamId),   // stream-scoped (§4)
        HostId = userId,
        StartedAt = DateTime.UtcNow,
    };

    try
    {
        var created = (await supabase
            .From<VideoStream>()
            .Insert(stream, new QueryOptions { Returning = ReturnType.Representation }, ct))
            .Models.FirstOrDefault()
            ?? throw new ConflictException("The video stream could not be created.");

        await UpsertParticipant(supabase, created, userId, VideoParticipant.RoleHost, ct);
        return created;
    }
    catch (PostgrestException ex) when (IsUniqueViolation(ex))
    {
        // Lost the race against a concurrent start — the same outcome the
        // pre-check would have produced a moment earlier.
        throw new ConflictException("This event already has a live video stream.");
    }
}

/// <summary>PostgreSQL 23505 — unique_violation.</summary>
private static bool IsUniqueViolation(PostgrestException ex) =>
    ex.Response?.Content?.Contains("23505", StringComparison.Ordinal) == true;
```

#### 6.3 Join — attendance, grace window, and multi-device

```csharp
/// <summary>
/// Role on (re)join:
///   - the stream host always resumes 'host';
///   - a participant reaped or dropped within the GRACE WINDOW resumes their prior
///     role, so a 70-second network blip does not cost a presenter the floor;
///   - anyone else enters as 'viewer'.
/// A duplicate join from a participant who never left is idempotent and MUST NOT
/// reset their role.
/// </summary>
private static readonly TimeSpan RejoinGrace = TimeSpan.FromMinutes(5);

public async Task<(VideoStream Stream, VideoParticipant Participant)> JoinEventStream(
    Supabase.Client supabase, Guid userId, Guid eventId, CancellationToken ct)
{
    var hostEvent = await GetOnlineEventOrThrow(supabase, eventId, ct);
    var stream = await GetLiveStreamOrThrow(supabase, eventId, ct);

    // Attendance is re-checked on EVERY join, including mid-stream, so a
    // non-attendee can never enter after the fact.
    if (!await IsEventAttendee(supabase, eventId, userId, ct))
        throw new ForbiddenException("Only users attending this event may join its video stream.");

    var existing = await supabase
        .From<VideoParticipant>()
        .Filter("video_stream_id", Operator.Equals, stream.Id.ToString())
        .Filter("participant_id", Operator.Equals, userId.ToString())
        .Single(ct);

    var now = DateTime.UtcNow;

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
            LastSeenAt = now,
        };
    }
    else if (existing.LeftAt != null)
    {
        var withinGrace = now - existing.LeftAt.Value <= RejoinGrace;
        var keepsRole = stream.HostId == userId
            || (withinGrace && existing.LeftReason != VideoParticipant.LeftDemoted);

        if (!keepsRole)
            existing.Role = VideoParticipant.RoleViewer;

        existing.LeftAt = null;
        existing.LeftReason = null;
        existing.SfuIdentity = null;
        existing.LastSeenAt = now;
    }
    else
    {
        // Live already: a second device. The SFU identity is per (stream, user), so
        // LiveKit disconnects the older connection — last device wins, one seat.
        existing.LastSeenAt = now;
    }

    await supabase
        .From<VideoParticipant>()
        .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal }, ct);

    return (stream, existing);
}
```

### 7. Reaper (`AlSaqr.API/HostedServices/VideoStreamReaperService.cs`)

A `BackgroundService` registered like `SpaceReaperService` (a **new** file; the space
reaper is not modified), scanning every ~30s. It owns three guarantees:

1. **Dead participants (C9).** `left_at IS NULL` and `last_seen_at` older than **60s**
   → `RemoveParticipantAsync`, stamp `left_at` + `left_reason = 'reaped'`, broadcast
   `participant_left`. This is the *"automatically disconnects the user"* guarantee.
2. **Empty streams (C3).** Zero un-left participants past the timeout → stamp
   `ended_at`, `DeleteRoomAsync`, broadcast `stream_ended`.
3. **Runaway streams (C11).** `started_at` older than **4 hours** → end regardless of
   activity, marking participants `left_reason = 'expired'`. Rules 1 and 2 cannot
   catch an occupied-but-abandoned room; this can.

```csharp
private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
private static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(60);
private static readonly TimeSpan MaxStreamDuration = TimeSpan.FromHours(4);   // C11

private async Task ReapOnce(CancellationToken ct)
{
    // IVideoStreamRepository is scoped and the SFU/broadcast services are typed
    // HttpClients — resolving them per scan keeps handler rotation working instead
    // of pinning one handler in this singleton.
    using var scope = _scopeFactory.CreateScope();
    var streams = scope.ServiceProvider.GetRequiredService<IVideoStreamRepository>();
    var livekit = scope.ServiceProvider.GetRequiredService<ILiveKitVideoService>();
    var broadcaster = scope.ServiceProvider.GetRequiredService<IEventVideoBroadcaster>();

    var now = DateTime.UtcNow;
    var cutoff = now - ParticipantTimeout;

    foreach (var stream in await streams.GetLiveStreams(_supabase, ct))
    {
        var expired = now - stream.StartedAt >= MaxStreamDuration;   // C11
        var live = await streams.GetLiveParticipants(_supabase, stream.Id, ct);

        var doomed = expired ? live : live.Where(p => p.LastSeenAt < cutoff).ToList();
        var reason = expired ? VideoParticipant.LeftExpired : VideoParticipant.LeftReaped;

        foreach (var participant in doomed)
        {
            var identity = participant.SfuIdentity;

            await streams.MarkParticipantLeft(_supabase, participant, reason, ct);

            if (!string.IsNullOrEmpty(identity))
                await livekit.RemoveParticipantAsync(stream.RoomName, identity, ct);

            await broadcaster.ParticipantLeftAsync(stream.EventId, participant.ParticipantId, reason, ct);
        }

        var remaining = expired ? 0 : (await streams.GetLiveParticipants(_supabase, stream.Id, ct)).Count;
        if (remaining == 0)
        {
            var ended = await streams.EndStreamSystem(_supabase, stream, ct);
            await livekit.DeleteRoomAsync(ended.RoomName, ct);
            await broadcaster.StreamEndedAsync(ended.EventId, ended.Id, ended.EndedAt!.Value, ct);
        }
    }
}
```

> **Optional hardening (MAY).** LiveKit can POST `participant_left` / `room_finished`
> webhooks, making disconnects near-instant instead of bounded by the 30s tick. That
> is an additive endpoint and is **out of scope**; the reaper alone satisfies the
> requirement.

### 8. Controller (`AlSaqr.API/Controllers/Meetup/EventVideoController.cs`)

A **new** controller — `SpacesController.cs` is not touched.
`EventVideoController : AuthorizedControllerBase`, `[Route("[controller]")]` (the
`api` prefix comes from `UseRoutePrefix`), constructor-injected `Supabase.Client`,
`IVideoStreamRepository`, `ILiveKitVideoService`, `IEventVideoBroadcaster`,
`ICallerIdentityAccessor`, `IUserCacheService`.

Every action calls `ValidateAccessToken()` (presence/expiry gate) **and** resolves the
caller via `ICallerIdentityAccessor.GetCallerId()` (§1.2). `IUserCacheService` is used
**only** for display fields, keyed by the verified id.

**All routes are event-scoped.** Exactly one stream is live per event, so `eventId`
addresses it unambiguously and clients never track two identifiers.

| Method | Route | Body | Returns |
|---|---|---|---|
| GET  | `api/EventVideo/event/{eventId}/live` | — | `VideoStreamToDisplay` or `null` |
| POST | `api/EventVideo/event/{eventId}/start` | `{ values: {} }` | `VideoStreamToDisplay` — organizer/founder only |
| POST | `api/EventVideo/event/{eventId}/join` | `{}` | `JoinVideoStreamResultDto` |
| POST | `api/EventVideo/event/{eventId}/leave` | `{}` | 204 |
| POST | `api/EventVideo/event/{eventId}/end` | `{}` | 204 — organizer/founder only |
| POST | `api/EventVideo/event/{eventId}/heartbeat` | `{}` | 204 — refreshes `last_seen_at` |
| POST | `api/EventVideo/event/{eventId}/camera` | `{ values: { cameraEnabled, muted } }` | 204 |
| POST | `api/EventVideo/event/{eventId}/presenters/{userId}/promote` | `{}` | 204 — organizer/founder only |
| POST | `api/EventVideo/event/{eventId}/presenters/{userId}/demote` | `{}` | 204 — organizer/founder only |

Not paginated — plain JSON bodies, no `pagination` header, no
`currentPage`/`itemsPerPage`/`searchTerm`.

> **Response envelope decision.** Meetup controllers wrap responses
> (`Ok(new { eventDetails, success = true })`) while `SpacesController` returns bare
> DTOs. This controller returns **bare DTOs**, matching the real-time feature it is
> modeled on and the `JoinVideoStreamResultDto` contract above. Rationale: the
> `success` envelope carries no information a status code does not, and a real-time
> client parsing tokens benefits from the flatter shape. This is a deliberate,
> recorded choice — not an oversight — and it MUST be applied consistently across all
> nine routes.

#### 8.1 Reference example — connecting to the video stream

```csharp
/// <summary>
/// Joins the live video stream of an online event. Returns the stream, the caller's
/// role, and a short-lived room-scoped LiveKit token: the browser connects with
/// { wsUrl, token } and never sees the API secret. Only users attending the event
/// get past the repository's attendance gate — a non-attendee is rejected with 403
/// before any SFU call is made and before any token is minted.
/// </summary>
[HttpPost("event/{eventId}/join")]
public async Task<IActionResult> JoinEventStream(Guid eventId)
{
    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;

    // Identity comes from the SIGNATURE-VERIFIED token, never from the shared
    // logged-in-user cache slot (§1.2, DEP-2/DEP-3).
    var userId = _caller.GetCallerId();
    if (userId == Guid.Empty)
        return Unauthorized("A verified access token is required.");
    if (eventId == Guid.Empty)
        return BadRequest("Missing required fields");

    var ct = HttpContext.RequestAborted;

    // Gate first: online event (C1) + attendance + live stream. Every 400/403/404
    // surfaces as a repository exception mapped by the global middleware, so no SFU
    // work and no token minting happen for an unauthorized caller.
    var (stream, participant) = await _videoStreams.JoinEventStream(_supabase, userId, eventId, ct);

    // Identity is stream-scoped, and so is the room name — together they make a
    // token from an earlier stream unusable here (§4).
    var identity = $"{stream.Id}:{userId}";
    var canPublish = participant.Role != VideoParticipant.RoleViewer;   // C4

    var profile = _userCacheService.GetLoggedInUser();
    var displayName = profile?.Id?.ToString() == userId.ToString()
        ? profile?.Username ?? userId.ToString()
        : userId.ToString();

    var token = _livekit.MintJoinToken(stream.RoomName, identity, displayName, canPublish);

    await _videoStreams.SetSfuIdentity(_supabase, eventId, userId, identity, ct);

    // "Indicate he's in the live": the roster row (left_at == null) is the state;
    // this broadcast is the notification other attendees react to.
    await _broadcaster.ParticipantJoinedAsync(eventId, userId, participant.Role, ct);

    var display = await _videoStreams.GetLiveEventStream(_supabase, eventId, ct)
        ?? throw new NotFoundException("The video stream ended while joining.");

    return Ok(new JoinVideoStreamResultDto
    {
        Stream = display,
        Role = participant.Role,
        WsUrl = token.WsUrl,
        Token = token.Token,
        ExpiresAt = token.ExpiresAt,
        CanPublish = canPublish,
        Participants = await _videoStreams.GetParticipants(_supabase, stream.Id, ct),
    });
}
```

#### 8.2 Reference example — disconnecting from the video stream

```csharp
/// <summary>
/// Leaves the event's video stream: force-disconnects the caller on the SFU and
/// announces participant_left. Idempotent — every client exit path (unmount, tab
/// close, explicit leave) calls it, and the reaper converges on the same steps for
/// a client that dies without calling it at all.
/// </summary>
[HttpPost("event/{eventId}/leave")]
public async Task<IActionResult> LeaveEventStream(Guid eventId)
{
    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;

    var userId = _caller.GetCallerId();
    if (userId == Guid.Empty)
        return Unauthorized("A verified access token is required.");
    if (eventId == Guid.Empty)
        return BadRequest("Missing required fields");

    var ct = HttpContext.RequestAborted;

    var stream = await _videoStreams.GetLiveEventStream(_supabase, eventId, ct);
    var participant = await _videoStreams.LeaveEventStream(
        _supabase, userId, eventId, VideoParticipant.LeftExplicit, ct);

    if (participant != null && stream != null)
    {
        // Removing the participant server-side is what actually stops the media;
        // trusting the client to close its own connection would leave the SFU
        // relaying video nobody watches (C9). The room name is READ from the
        // stream — never recomputed, since it is stream-scoped (§4).
        if (!string.IsNullOrEmpty(participant.SfuIdentity))
            await _livekit.RemoveParticipantAsync(stream.RoomName, participant.SfuIdentity, ct);

        await _broadcaster.ParticipantLeftAsync(
            eventId, userId, VideoParticipant.LeftExplicit, ct);
    }

    return NoContent();
}
```

#### 8.3 Demote — the revocation that actually revokes

```csharp
/// <summary>
/// Demotes a presenter → viewer. Flipping the persisted role is not enough: the
/// user still holds a signed token with canPublish = true until it expires.
/// UpdateParticipant revokes the permission on the SFU itself, effective
/// immediately, so the floor is taken back the moment the host asks.
/// </summary>
[HttpPost("event/{eventId}/presenters/{userId}/demote")]
public async Task<IActionResult> DemotePresenter(Guid eventId, Guid userId)
{
    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;

    var callerId = _caller.GetCallerId();
    if (callerId == Guid.Empty)
        return Unauthorized("A verified access token is required.");
    if (eventId == Guid.Empty || userId == Guid.Empty)
        return BadRequest("Missing required fields");

    var ct = HttpContext.RequestAborted;

    var stream = await _videoStreams.GetLiveEventStream(_supabase, eventId, ct)
        ?? throw new NotFoundException("No live video stream for this event.");

    var demoted = await _videoStreams.DemotePresenter(_supabase, callerId, eventId, userId, ct);

    if (!string.IsNullOrEmpty(demoted.SfuIdentity))
    {
        await _livekit.UpdatePublishPermissionAsync(
            stream.RoomName, demoted.SfuIdentity, canPublish: false, ct);
    }

    await _broadcaster.RoleChangedAsync(eventId, userId, VideoParticipant.RoleViewer, ct);

    return NoContent();
}
```

Remaining orchestration (controller → repo/service; no Supabase or LiveKit calls
inline beyond the injected services):

- **Start**: repository validates online + group + authority + no live stream →
  `CreateRoomAsync(stream.RoomName, config.MaxParticipants)` → broadcast
  `stream_started`.
- **End**: repository stamps `ended_at`, marks everyone left with reason `ended` →
  `DeleteRoomAsync(stream.RoomName)` → broadcast `stream_ended`.
- **Promote**: flip role (cap-checked, C5) → `UpdatePublishPermissionAsync(true)` →
  broadcast `role_changed`. The client re-joins to obtain a publish token.
- **Heartbeat**: refreshes `last_seen_at`. The client **MUST** call it every ~20s,
  well under the 60s timeout.

### 9. Retention (`AlSaqr.API/HostedServices` — daily job)

No media is ever stored, but `video_participants` records **who was in a call with
whom, when, and for how long** — a timestamped association graph across community
events. Ephemerality **MUST** extend to it:

- `video_participants` rows are deleted **90 days** after `left_at`.
- `video_streams` rows are deleted once no participant rows reference them.
- Account deletion is already covered: `ON DELETE CASCADE` on
  `participant_id → users(id)`.

The purge runs daily in the reaper's hosted service via
`PurgeParticipantsLeftBefore(cutoff)`. The join UI **MUST** state plainly: *"This call
is not recorded. We keep a record of who joined and for how long for 90 days."*

### 10. Error contract

Every failure below is produced by a repository exception and mapped by
`ExceptionHandlingMiddleware` (DEP-1). Controllers **MUST NOT** try/catch for HTTP
mapping (§3.3).

| Condition | Exception | Status |
|---|---|---|
| Event does not exist / no live stream | `NotFoundException` | 404 |
| Event is in-person (C1), or has no host group | `ValidationException` | 400 |
| Not an attendee / not organizer-or-founder / not a participant | `ForbiddenException` | 403 |
| Second live stream; presenter cap reached (C5) | `ConflictException` | 409 |

```json
// 403 — application/problem+json (RFC 7807)
{
  "type": null,
  "title": "Forbidden",
  "status": 403,
  "detail": "Only users attending this event may join its video stream.",
  "instance": "/api/EventVideo/event/9f1c.../join"
}
```

> **401 is shaped differently, by design.** `ValidateAccessToken()` returns
> `Unauthorized(error)` — a bare JSON string, **not** ProblemDetails — because the
> gate runs before the middleware can classify anything. Clients **MUST** handle 401
> as a plain string body and 4xx/5xx as ProblemDetails. This asymmetry is inherited
> from `access-token.md` and is documented here so clients are not surprised.

### 11. Local development

The SFU runs locally in Docker; the API and React client talk to it over localhost.
No cloud account and no paid service are involved.

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

```json
// appsettings.Development.json — --dev uses the well-known devkey/secret pair
{
  "LiveKit": {
    "ApiKey": "devkey",
    "ApiSecret": "secret",
    "HttpUrl": "http://localhost:7880",
    "WsUrl": "ws://localhost:7880",
    "MaxParticipants": 50,
    "MaxPresenters": 9
  }
}
```

```ts
// The token and wsUrl come from POST api/EventVideo/event/{eventId}/join
const room = new Room({
  adaptiveStream: true,   // downscale subscriptions to the rendered size
  dynacast: true,         // C6 — stop publishing layers nobody subscribes to
  publishDefaults: { simulcast: true },
});

await room.connect(wsUrl, token);
if (canPublish) await room.localParticipant.enableCameraAndMicrophone();

// Heartbeat well under the 60s reaper timeout (C9).
const beat = setInterval(
  () => api.post(`api/EventVideo/event/${eventId}/heartbeat`, {}), 20_000);

// Disconnect: call the API first so the backend is authoritative, then drop the
// peer connection. Calling only room.disconnect() leaves the roster stale until
// the reaper runs.
clearInterval(beat);
await api.post(`api/EventVideo/event/${eventId}/leave`, {});
await room.disconnect();
```

### 12. DI registration (`Program.cs`)

```csharp
// Online-event video streaming (specs/video-streaming-online-events.md):
// verified caller identity, repository, self-hosted LiveKit control plane, realtime
// broadcaster, and the reaper/purge host. The LiveKit API secret stays server-side.
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<SupabaseAuthConfig>(builder.Configuration.GetSection("Supabase"));
builder.Services.AddScoped<ICallerIdentityAccessor, CallerIdentityAccessor>();

builder.Services.AddScoped<IVideoStreamRepository, VideoStreamRepository>();
builder.Services.Configure<LiveKitConfig>(builder.Configuration.GetSection("LiveKit"));
builder.Services.AddHttpClient<ILiveKitVideoService, LiveKitVideoService>();
builder.Services.AddHttpClient<ISupabaseBroadcastClient, SupabaseBroadcastClient>();
builder.Services.AddScoped<IEventVideoBroadcaster, EventVideoBroadcaster>();
builder.Services.AddHostedService<VideoStreamReaperService>();
```

All against interfaces (§3.1). No code is added to `AlSaqr.Services` (§2).

---

## Testing

### Harness

Repositories call `supabase.From<T>()` over **PostgREST HTTP**, so a bare Postgres
container cannot serve them — the container set needs PostgREST in front of Postgres,
with the `Supabase.Client` pointed at it. Faking `IVideoStreamRepository` instead
would test nothing, because the authorization logic *lives in the repository*.

```csharp
/// <summary>
/// Throwaway Postgres + PostgREST per run (CLAUDE.md §Validation): no shared dev DB,
/// no manual setup. PostgREST is required because the repositories speak PostgREST,
/// not raw SQL — a Postgres-only fixture cannot exercise them.
/// </summary>
public sealed class VideoStreamFixture : IAsyncLifetime
{
    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly PostgreSqlContainer _db;
    private readonly IContainer _postgrest;

    public Supabase.Client Client { get; private set; } = default!;
    public FakeLiveKitVideoService LiveKit { get; } = new();
    public FakeEventVideoBroadcaster Broadcaster { get; } = new();

    public VideoStreamFixture()
    {
        _db = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithNetwork(_network)
            .WithNetworkAliases("db")
            .Build();

        _postgrest = new ContainerBuilder()
            .WithImage("postgrest/postgrest:v12.2.0")
            .WithNetwork(_network)
            .WithPortBinding(3000, true)
            .WithEnvironment("PGRST_DB_SCHEMAS", "alsaqr-2026")
            .WithEnvironment("PGRST_DB_ANON_ROLE", "postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();
        await _db.StartAsync();
        await ApplySchema(_db, "Entities/Meetup/sql/video_streams.sql");   // + referenced tables
        await _postgrest.StartAsync();

        Client = new Supabase.Client(
            $"http://localhost:{_postgrest.GetMappedPublicPort(3000)}",
            null,
            new SupabaseOptions { Schema = "alsaqr-2026" });
    }

    public async Task DisposeAsync()
    {
        await _postgrest.DisposeAsync();
        await _db.DisposeAsync();
        await _network.DeleteAsync();
    }
}
```

Each test runs inside a transaction rolled back on completion. `ILiveKitVideoService`
and `IEventVideoBroadcaster` are faked and **record their calls** — several assertions
below are about a call *not* happening.

### Required test cases

**Authorization (the primary surface)**

| # | Test | Assertion |
|---|---|---|
| A1 | `Start_InPersonEvent_Returns400_AndNoSfuCall` | `ValidationException`; `LiveKit.Calls` is **empty** |
| A2 | `Start_GrouplessEvent_Returns400` | `ValidationException` |
| A3 | `Start_NonOrganizer_Returns403` | `ForbiddenException` |
| A4 | `Start_EventOrganizer_CreatesStreamAndRoom` | stream persisted; `CreateRoom` called with `emptyTimeout=120`, `maxParticipants=50` |
| A5 | `Start_GroupFounder_Succeeds` | founder authority honoured (§6.1) |
| A6 | `Start_SecondLiveStream_Returns409` | `ConflictException` |
| A7 | `Start_ConcurrentDoubleStart_OneWins_OtherGets409` | two parallel calls → exactly one stream row; loser gets `ConflictException`, **not** 500 |
| A8 | `Join_NonAttendee_Returns403_AndNoTokenMinted` | `ForbiddenException`; `LiveKit.MintedTokens` is **empty** |
| A9 | `Join_Attendee_ReturnsTokenAndRoster` | token non-empty; roster contains the caller |
| A10 | `Join_AfterStreamEnded_Returns404` | `NotFoundException` |
| A11 | `Promote_ByNonHost_Returns403` | `ForbiddenException` |
| A12 | `Promote_BeyondPresenterCap_Returns409` | 9 presenters exist → `ConflictException` (C5) |
| A13 | `End_ByNonHost_Returns403` | `ForbiddenException` |

**Token grant (the permission model)**

| # | Test | Assertion |
|---|---|---|
| T1 | `ViewerToken_CannotPublish` | decoded `video.canPublish == false`; `canPublishSources` empty |
| T2 | `PresenterToken_CanPublishCameraAndMicOnly` | `canPublish == true`; sources exactly `["camera","microphone"]` |
| T3 | `Token_IsScopedToOneRoomAndExpiresWithin90s` | `video.room == stream.RoomName`; `exp − nbf ≤ 90` |
| T4 | `Token_FromEndedStream_DoesNotMatchNewStreamRoom` | new stream's `RoomName` ≠ old — the replay guard of §4 |
| T5 | `Demote_RevokesPublishOnSfu` | `UpdateParticipant(canPublish:false)` recorded; role flipped |

**Lifecycle**

| # | Test | Assertion |
|---|---|---|
| L1 | `Join_MarksParticipantLive_AndBroadcasts` | `left_at IS NULL`; `participant_joined` broadcast |
| L2 | `Leave_RemovesOnSfu_AndBroadcasts` | `RemoveParticipant` recorded; `left_reason == 'explicit'` |
| L3 | `Leave_Twice_IsNoOp` | second call → 204, no second SFU call |
| L4 | `Reaper_EvictsStaleParticipant` | `last_seen_at` 61s old → `left_reason == 'reaped'`, `RemoveParticipant` recorded |
| L5 | `Reaper_DoesNotEvictHeartbeatingParticipant` | fresh `last_seen_at` → untouched |
| L6 | `Reaper_EndsEmptyStream` | zero live → `ended_at` set, `DeleteRoom` recorded, `stream_ended` broadcast |
| L7 | `Reaper_EndsStreamPastFourHours_EvenWhenOccupied` | C11; participants marked `expired` |
| L8 | `Rejoin_WithinGrace_KeepsPresenterRole` | reaped presenter rejoins at 4 min → still `presenter` |
| L9 | `Rejoin_AfterGrace_DowngradesToViewer` | rejoin at 6 min → `viewer` |
| L10 | `Rejoin_AfterDemotion_DoesNotRestorePresenter` | `left_reason == 'demoted'` is never restored |
| L11 | `SecondDevice_DoesNotCreateSecondRow` | one row per `(stream, user)` |
| L12 | `Purge_DeletesParticipantsOlderThan90Days` | 91-day-old row gone; 89-day-old row kept |

**Ordering & contract**

| # | Test | Assertion |
|---|---|---|
| C1 | `Roster_IsDeterministicallyOrdered` | `joined_at` asc, `id` asc, stable across repeated reads |
| C2 | `JoinResult_SerializesToContractShape` | camelCase keys exactly as §3; `videoStreamId` not `id` |
| C3 | `NoEgressOrRecordingCallSiteExists` | source scan for `egress`/`recording`/`transcription` in the video namespace returns **zero** hits (C10) |

---

## Rules

**Authorization (backend-authoritative — the client never decides who may publish)**

- Video streaming exists **only** for events with `is_online = true` and a host group.
  Anything else is **400**, rejected before any SFU call (C1).
- **Only users attending the event may join** — verified through `attendees` →
  `event_attendees` on **every** join, including mid-stream. Non-attendees: **403**.
- Only the **event organizer** (`is_event_organizer`) or the **host group's founder**
  (`groups.founder_id`) may start, end, promote, or demote. Founder-first resolution.
  Anyone else: **403**.
- **One live stream per event**; a second start is **409**, enforced by the partial
  unique index and by explicit unique-violation translation (§6.2), not by the
  pre-check alone.
- Publish rights are enforced **twice**: `canPublish` in the signed token at join
  time, and `UpdateParticipant` for mid-session revocation. A role flip that is not
  accompanied by an SFU permission update is a defect.
- The presenter cap (C5) and occupancy cap (C12) are enforced in the repository and
  on `CreateRoom` respectively.
- Every endpoint requires a valid access token **and** a signature-verified caller id
  (§1.2). Identity **MUST NOT** be read from the shared logged-in-user cache slot.

**SFU control plane**

- The LiveKit **API key and secret MUST NOT touch the browser**. Only `wsUrl` and a
  minted, room-scoped, ≤90-second token leave the API.
- Room names are **stream-scoped and globally unique**; identities are
  `{videoStreamId}:{userId}`. Room names are **read from `video_streams.room_name`**,
  never recomputed at a call site.
- Disconnects are **server-issued**. A client closing its own peer connection is a
  hint, not a teardown.
- Every exit path converges on the same teardown — explicit leave, host end, C11
  expiry, and the reaper all remove the participant on the SFU, record a
  `left_reason`, and broadcast.

**Presence & events (channel `event_video:{eventId}`)**

- Presence is Supabase, media is LiveKit — never conflate them.
- Backend-emitted (service-role key): `stream_started`, `stream_ended`,
  `participant_joined`, `participant_left`, `role_changed`.
- Client-emitted (backend neither emits nor validates): camera/mic track events,
  which LiveKit already surfaces to every subscriber.

**Ephemerality**

- Streams are never recorded. **No egress, capture, or transcription call may exist
  anywhere in the code** (C10) — asserted by test C3.
- Participation metadata is retained **90 days** and then purged (§9). "We don't
  record" is only true if the metadata is bounded too.

**Constitution compliance (CLAUDE.md)**

- Controllers never touch the Supabase or LiveKit clients directly beyond injected
  services/repos (§3.2); `Supabase.Client` is passed per method, never stored (§3.1);
  DI against interfaces only (§3.1).
- Write failures throw custom exceptions **inside the repository**; HTTP mapping
  lives in the global middleware only (§3.3) — which requires DEP-1.
- **Video endpoints are exempt from §4.1 caching** (this document is the exemption):
  live lookups, rosters, and every mutation are real-time state, always read live.
  Nothing is cached, so nothing needs invalidation.
- `System.Text.Json` only (§4); every async DB/HTTP call awaited (§4).
- All list-ish reads use deterministic ordering (`joined_at` asc, `id` asc) (§3.2).
- No code is added to `AlSaqr.Services` (§2).
- Two documented deviations: entity `Schema` attribute (§2) and the broadcast-client
  duplication (§5). Both are deliberate, justified, and recorded.

---

## Acceptance

- **Contract**: all nine routes exist with the exact method, path, envelope, and
  response shape; DTOs serialize camelCase per §3; no pagination header; bare-DTO
  responses applied consistently.
- **Authorization matrix**: tests A1–A13 pass, including A7 (concurrent start → one
  409, never a 500) and A8 (**no token minted** for a non-attendee).
- **Token grant**: T1–T5 pass — a viewer token that could publish is a security
  defect, not a cosmetic one.
- **"He's in the live"**: L1 — roster row with `left_at IS NULL` plus a
  `participant_joined` broadcast.
- **"Automatically disconnects"**: L4, L6, L7 — reaped at 60s, empty stream ended,
  4-hour ceiling honoured even when occupied.
- **Not at the cost of a live user**: L5, L8–L10 — a heartbeating user is never
  evicted, and a 5-minute reconnect preserves the presenter's floor.
- **Cost guards**: `CreateRoom` receives `emptyTimeout=120` and configured
  `maxParticipants`; the client connects with dynacast + simulcast; C11 enforced.
- **Ephemerality**: C3 (no egress call site) and L12 (90-day purge) pass.
- **Secrets**: LiveKit key/secret and the Supabase service-role key come from
  configuration/AWS secrets and never appear in any response, log, or client-visible
  error.
- **Isolation**: `git diff` for this feature touches no audio-spaces file.
- **Observability**: `left_reason` populated on every departure, so involuntary
  disconnect rate is `count(left_reason='reaped') ÷ count(*)` — one query, no vendor.
- Deterministic tests, runnable in CI, no shared dev DB (§Validation).

---

## Out of Scope

- Recording, transcription, or video persistence of any kind (C10).
- Video for **in-person** events; group-level (non-event) video.
- Screen sharing, virtual backgrounds, breakout rooms, chat overlay, reactions.
- Raise-hand → auto-promote; promotion is an explicit host action.
- LiveKit webhooks (an optional latency improvement over the reaper, §7).
- SFU scaling, cascading, region routing, TURN provisioning beyond a single node.
- Any change to audio spaces, including migrating `SpaceEventBroadcaster` onto the
  shared `SupabaseBroadcastClient` (follow-up, §5).
- Fixing signature verification **globally** for all endpoints — §1.2 closes it for
  this feature; `access-token.md` still governs the rest of the API.
- Native mobile clients.

---

## Changelog — v1 → v2

| Defect (v1) | Resolution (v2) |
|---|---|
| Room name event-scoped → tokens replayable across streams; demote defeated by a still-valid token | Stream-scoped, DB-unique room names; `UpdateParticipant` server-side revocation; TTL 10 min → 90 s (§4, T4, T5) |
| Identity from the shared `UserCacheService` slot; v1 suggested the JWT `sub` — but the signature is **never verified** | `ICallerIdentityAccessor` verifies HS256 before trusting `sub`; cache demoted to profile lookup (§1.2, DEP-2/DEP-3) |
| `RoomNameFor(eventId)` recomputed at the leave call site | Room name always read from `video_streams.room_name` (§8.2) |
| `maxParticipants` referenced but never defined | `LiveKitConfig.MaxParticipants` = 50, `MaxPresenters` = 9 (§1.1, C5, C12) |
| "Organizer" undefined; two competing authorities in code | Founder-first `CanControlEventVideo`; groupless events → 400 (§6.1) |
| No concrete tests; Testcontainers infeasible against PostgREST | Postgres + PostgREST fixture; 33 named test cases (§Testing) |
| Start pre-check is TOCTOU → 500 on race | Unique-violation → `ConflictException` (§6.2, A7) |
| Response envelope conflict with Meetup controllers unacknowledged | Explicit, justified bare-DTO decision (§8) |
| No ProblemDetails example; 401's differing shape undocumented | Error contract table + example + 401 note (§10) |
| Multi-device join undefined | Last-device-wins, one row per (stream, user) (§6.3, L11) |
| No max stream duration | C11 four-hour ceiling (§7, L7) |
| `?? new()` masked a 404 in the join example | Throws `NotFoundException` (§8.1) |
| DTOs/broadcaster/repository/reaper described in prose only | Full C# for all four (§3, §5, §6, §7) |
| Mixed `eventId` / `videoStreamId` addressing | All routes event-scoped (§8) |
| `event_id` denormalization could drift from the stream | Composite FK `(video_stream_id, event_id)` (§2) |
| Involuntary disconnects unmeasurable | `left_reason` column + constraint (§2, §10) |
| Network blip cost a presenter their role | 5-minute reconnect grace (§6.3, L8–L10) |
| Participation metadata retained forever | 90-day purge + disclosure copy (§9, L12) |
| `[Table]` schema deviation silent | Documented and justified (§2) |
