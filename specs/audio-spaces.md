# Specification: Audio Spaces (Ephemeral Twitter-Spaces Clone)

> **Backend counterpart of the frontend spec**
> `alsaqr-frontend-v2/specs/replace-virtualization-audio-discussions.md`.
> The React client is already implemented; this backend work **MUST** match its
> contract exactly — routes, body shapes, and DTO field names are not negotiable.
> Conformance keywords (**MUST**, **MUST NOT**, **SHOULD**, **MAY**) follow RFC 2119.
>
> Convention note: the frontend prompt mentions "MediatR-style handlers and EF
> migrations" — this solution uses neither. Per the project constitution
> (`CLAUDE.md`), data access goes through Supabase PostgREST **repositories** with
> the `Supabase.Client` passed per method, and table DDL lives as `.sql` files
> under `AlSaqr.Data/Entities/*/sql`. This spec follows the actual conventions.

---

## Overview

Communities and community discussions get live, **ephemeral** audio rooms
("spaces"), modeled on Twitter Spaces:

- A community member starts a space and becomes its **host**. Other members join
  as **listeners**; a listener may raise a hand and be promoted by the host to
  **speaker**. The host may demote speakers back to listener and is the only one
  who can end the space.
- **Media** is WebRTC audio through **Cloudflare Calls (Realtime SFU)**. The
  backend is a pure **proxy** for every SFU interaction — the Cloudflare app
  secret never reaches the browser, and the client never talks to Cloudflare
  HTTP directly. The client only sets STUN (`stun:stun.cloudflare.com:3478`);
  TURN is Cloudflare's concern.
- **Presence** is the Supabase realtime channel `space:{spaceId}` (presence +
  broadcast). Presence is Supabase, media is Cloudflare — the two are
  independent truths and **MUST NOT** be conflated. The backend publishes
  authoritative events (`role_changed`, `space_ended`) to that channel using the
  Supabase **service-role-secret** key.
- **Ephemerality is guaranteed by omission**: the backend never calls any
  Cloudflare egress, capture, or transcription API — no such code path may
  exist. Only `started_at` / `ended_at` metadata is persisted; after a space
  ends, no audio artifact exists in any store.
- The backend is **authoritative for roles and permissions**. The frontend only
  reflects role state; a client flipping its own role has no audio effect until
  the backend actually grants a publish.

Two space kinds:

| Kind | Serialized as | Who may start / join |
|---|---|---|
| Community space | `community` | community members (`member` / `moderator` / `founder`) |
| Community-discussion space | `community-discussion` | members of that discussion |

---

## Implementation Steps

### 1. Configuration (`AlSaqr.API/Config/AppSecrets.cs` + AWS secrets)

- Add `CloudflareCallsSettings { AppId, AppSecret, BaseUrl }` to `AppSecrets`.
- Add a `ServiceRoleKey` to `SupabaseSettings` (used **only** server-side to
  broadcast on `space:{spaceId}`; the existing anon `Key` stays for the normal
  client).
- Both secrets flow through the existing AWS Secrets Manager →
  `AddInMemoryCollection` path in `Program.cs`. Neither value may ever appear in
  a response body, log line, or client-visible error.

### 2. Tables + entities (`AlSaqr.Data/Entities/SocialMedia`)

Follow the existing entity conventions (`BaseModel`, `[Table]` /
`[PrimaryKey]` / `[Column]`, snake_case plural table names — see
`CommunityMember.cs`). Add the DDL as `.sql` files under
`Entities/SocialMedia/sql/`.

**`spaces` → `Space` entity**

| Column | C# | Notes |
|---|---|---|
| `id` | `Guid Id` | PK |
| `kind` | `string Kind` | `community` \| `community-discussion` |
| `community_id` | `Guid CommunityId` | always set |
| `community_discussion_id` | `Guid? CommunityDiscussionId` | set only for discussion spaces |
| `title` | `string Title` | |
| `host_id` | `Guid HostId` | |
| `started_at` | `DateTime StartedAt` | |
| `ended_at` | `DateTime? EndedAt` | null while live; `IsLive == EndedAt is null` |

**`space_participants` → `SpaceParticipant` entity**

| Column | C# | Notes |
|---|---|---|
| `id` | `Guid Id` | PK |
| `space_id` | `Guid SpaceId` | |
| `user_id` | `Guid UserId` | |
| `role` | `string Role` | `host` \| `speaker` \| `listener` |
| `muted` | `bool Muted` | |
| `hand_raised` | `bool HandRaised` | |
| `sfu_session_id` | `string? SfuSessionId` | set while publishing |
| `track_name` | `string? TrackName` | set while publishing |
| `joined_at` | `DateTime JoinedAt` | |
| `left_at` | `DateTime? LeftAt` | null while present |
| `last_seen_at` | `DateTime LastSeenAt` | refreshed by any authenticated call from the participant; drives the reaper (step 7) |

The one-live-space-per-community / per-discussion invariant **MUST** be enforced
in the database (partial unique index on `community_id` /
`community_discussion_id` where `ended_at IS NULL`), not only in application
code.

### 3. DTOs (`AlSaqr.Domain`)

Follow the existing pattern (static class with nested DTOs, e.g.
`AlSaqr.Domain.SocialMedia.CommunityDiscussion`). All DTOs serialize with the
default System.Text.Json camelCase policy and **MUST** produce exactly these
TypeScript shapes:

```typescript
type SpaceKind = 'community' | 'community-discussion';
type SpaceRole = 'host' | 'speaker' | 'listener';

interface SpaceToDisplay {
  spaceId: string; kind: SpaceKind; communityId: string; communityDiscussionId?: string;
  title: string; hostId: string; hostUsername?: string; hostAvatar?: string;
  startedAt: string; endedAt?: string; participantCount: number; isLive: boolean;
}

interface SpaceParticipant {
  userId: string; username: string; avatar?: string;
  role: SpaceRole; muted: boolean; handRaised: boolean;
  sfuSessionId?: string; trackName?: string; // set for publishing speakers
}

interface JoinSpaceResultDto {
  space: SpaceToDisplay; role: SpaceRole;
  participants: SpaceParticipant[];
  speakers: SpaceParticipant[]; // currently publishing, so the joiner can pull each track
}
```

Request DTOs (each arrives wrapped in the existing
`AlSaqrUpsertRequest<T>` `{ values: ... }` envelope): `StartSpaceForm { Title }`,
`PublishForm { Sdp, Mid }`, `SubscribeForm { PubSessionId, TrackName }`,
`RenegotiateForm { SessionId, Sdp }`, `RaiseHandForm { Raised }`.

### 4. Cloudflare SFU proxy service (`AlSaqr.Infrastructure`)

`ICloudflareCallsService`, registered as a typed `HttpClient`
(`AddHttpClient<ICloudflareCallsService, CloudflareCallsService>`), holding the
app id/secret from configuration. Methods (thin wrappers over the Cloudflare
Calls HTTP API — `sessions/new`, `sessions/{id}/tracks/new`,
`sessions/{id}/renegotiate`, track close):

- `CreateSessionWithPublishedTrackAsync(sdpOffer, mid)` → `(sessionId, trackName, sdpAnswer)`
- `PullTrackAsync(callerSessionId?, pubSessionId, trackName)` → `(sessionId, sdpOffer)`
- `RenegotiateAsync(sessionId, sdpAnswer)`
- `CloseTracksAsync(sessionId)` / close session — used on leave, demote, end, reap.

The service **MUST NOT** contain any call to egress, recording, or
transcription endpoints — those code paths must not exist (see Rules).

### 5. Supabase realtime broadcaster (`AlSaqr.Infrastructure`)

`ISpaceEventBroadcaster` using the **service-role-secret** key to broadcast on
`space:{spaceId}`:

- `role_changed { userId, role }` — after approve / demote.
- `space_ended { spaceId, endedAt }` — after end (host action or reaper).
- `track_added { userId, sfuSessionId, trackName }` — after a successful publish.
- `track_closed { userId }` — after leave / demote / reap unpublishes a track.

(`mute_changed` and `hand_raised` remain client-emitted; the backend does not
re-broadcast them.)

### 6. Repository (`AlSaqr.Data/Repositories/SocialMedia`)

`ISpaceRepository` + `Impl/SpaceRepository`, `Supabase.Client` passed per method
(§3.1). Responsibilities: live-space lookup per community / per discussion,
create-space (rejecting a second live space → `ConflictException`), membership
checks (reuse `CommunityMember` / `CommunityDiscussionMember` queries),
participant upsert on join, role transitions, hand state, SFU session/track
column updates, leave/end stamping. Custom exceptions (`NotFoundException`,
`ValidationException`, `ConflictException`, `ForbiddenException`) are thrown
**inside the repository** for failed writes per §3.3.

### 7. Reaper (`AlSaqr.API` hosted service)

A `BackgroundService` (registered like `SupabaseInitializer`) that periodically
(every ~30s) scans live spaces:

- A participant with `left_at IS NULL` whose `last_seen_at` is older than the
  timeout (default **60s**) is reaped: close their Cloudflare session/tracks,
  stamp `left_at`, broadcast `track_closed { userId }`.
- A live space with zero un-left participants past the timeout is ended: stamp
  `ended_at`, broadcast `space_ended`.

This guarantees the SFU stops pulling audio nobody hears after a network death
or tab close without an explicit leave.

### 8. Controller (`AlSaqr.API/Controllers/SocialMedia/SpacesController.cs`)

`SpacesController : AuthorizedControllerBase`, `[Route("[controller]")]` (the
`api` prefix comes from `UseRoutePrefix`), constructor-injected
`Supabase.Client`, `ISpaceRepository`, `ICloudflareCallsService`,
`ISpaceEventBroadcaster`, `IUserCacheService`. Every action validates the token
via `ValidateAccessToken()` and resolves the caller from
`IUserCacheService.GetLoggedInUser()` (never an ad-hoc re-fetch, §4.1).

Routes — **must match the frontend exactly**; POST bodies are
`{ values: ... }` except join/leave/end/approve/demote which send empty `{}`:

| Method | Route | Body | Returns |
|---|---|---|---|
| GET  | `api/Spaces/community/{communityId}/live` | — | `SpaceToDisplay` or `null` |
| GET  | `api/Spaces/community/{communityId}/discussion/{communityDiscussionId}/live` | — | `SpaceToDisplay` or `null` |
| POST | `api/Spaces/community/{communityId}` | `{ values: { title } }` | `SpaceToDisplay` — caller becomes host |
| POST | `api/Spaces/community/{communityId}/discussion/{communityDiscussionId}` | `{ values: { title } }` | `SpaceToDisplay` |
| POST | `api/Spaces/{spaceId}/join` | `{}` | `JoinSpaceResultDto` |
| POST | `api/Spaces/{spaceId}/leave` | `{}` | 204 |
| POST | `api/Spaces/{spaceId}/end` | `{}` | 204 — host only |
| POST | `api/Spaces/{spaceId}/publish` | `{ values: { sdp, mid } }` | `{ sessionId, trackName, answer }` |
| POST | `api/Spaces/{spaceId}/subscribe` | `{ values: { pubSessionId, trackName } }` | `{ sessionId, offer }` |
| POST | `api/Spaces/{spaceId}/renegotiate` | `{ values: { sessionId, sdp } }` | 204 |
| POST | `api/Spaces/{spaceId}/raise-hand` | `{ values: { raised } }` | 204 |
| POST | `api/Spaces/{spaceId}/speakers/{userId}/approve` | `{}` | 204 — host only |
| POST | `api/Spaces/{spaceId}/speakers/{userId}/demote` | `{}` | 204 — host only |

These endpoints are **not paginated** — plain JSON bodies, no `pagination`
header, no `currentPage`/`itemsPerPage`/`searchTerm` query params.

SFU flow orchestration (controller → repo/service, no Supabase or Cloudflare
calls inline beyond the injected services):

- **Publish**: verify caller's persisted role is `host` or `speaker` *at call
  time* → forward SDP offer to Cloudflare (client OFFERS, SFU ANSWERS) → store
  `sfu_session_id` + `track_name` on the participant row → return
  `{ sessionId, trackName, answer }` → broadcast `track_added`.
- **Subscribe**: any live participant of the space → ask Cloudflare to pull
  `trackName` from `pubSessionId` into the caller's (new or existing) session
  (SFU OFFERS) → return `{ sessionId, offer }`. Each subscribe is scoped to one
  track so adding the nth speaker never disturbs existing subscriptions.
- **Renegotiate**: forward the client's SDP answer to Cloudflare **in order**;
  ordering is owned by the client, the backend just forwards. Returns 204.
- **Leave**: close the participant's Cloudflare session(s)/tracks, stamp
  `left_at`, clear SFU columns, broadcast `track_closed { userId }`.
- **End** (host only): close every participant's sessions/tracks, stamp
  `ended_at`, broadcast `space_ended { spaceId, endedAt }`.
- **Approve / demote** (host only): flip the persisted role, broadcast
  `role_changed { userId, role }` (the promoted client runs publish when it
  receives this). Demote additionally closes the demoted user's published track
  and broadcasts `track_closed`.

### 9. DI registration (`Program.cs`)

`ISpaceRepository` scoped; `ICloudflareCallsService` and
`ISpaceEventBroadcaster` via `AddHttpClient<,>` / singleton as appropriate; the
reaper via `AddHostedService<>`. All against interfaces (§3.1).

### 10. Integration tests

Per §Validation (Testcontainers or dedicated test schema with per-test
rollback; no shared dev DB). Cloudflare is faked behind
`ICloudflareCallsService`; broadcasts asserted through a fake
`ISpaceEventBroadcaster`. The authorization matrix (Rules below) is the primary
test surface.

---

## Rules

**Authorization (backend-authoritative — the frontend never decides who may speak)**

- Community spaces: only community members (`member` / `moderator` / `founder`)
  may start or join. Non-members get **403** — including while a space is live.
- Community-discussion spaces: only members of that discussion may start or join.
- **One live space per community and per discussion** at a time; starting a
  second is a **409** (`ConflictException`), enforced by the DB index.
- Only the **host** may end a space, approve a raised hand
  (listener → speaker), or demote a speaker → listener. Anyone else: **403**.
- **Publish is rejected (403) unless the caller's persisted role is `host` or
  `speaker` at the time of the publish call.** A client flipping its own role
  has no audio effect until the backend has actually granted the publish.
- Listeners are receive-only; **subscribe is available to any participant** of
  the space (and only participants).
- Every endpoint requires a valid Supabase JWT (`ValidateAccessToken()`); the
  logged-in user comes from `UserCacheService`, never an ad-hoc re-fetch (§4.1).

**SFU proxying**

- The Cloudflare app secret **MUST NOT** touch the browser; every SFU
  interaction is proxied. No direct client↔Cloudflare HTTP exists.
- Directionality is fixed and MUST NOT be mixed up: **publish = client offers,
  SFU answers; subscribe = SFU offers, client answers via `renegotiate`.**
- Renegotiation ordering is strict and owned by the client; the backend forwards
  in the order received, no reordering.
- Mute is local + broadcast only — the backend **does not unpublish on mute**.
  Tracks are unpublished only on leave, demotion, end, or reap.
- Every exit path converges on the same teardown: explicit leave, host end, and
  reaper all close Cloudflare sessions/tracks and broadcast the corresponding
  event. A silently dead participant (network death, tab close) **MUST** be
  reaped within the timeout so the SFU stops pulling audio nobody hears.

**Presence & events (channel `space:{spaceId}`)**

- Presence is Supabase, media is Cloudflare — never conflate them; an SFU track
  is not proof of membership and vice versa.
- Backend-emitted (service-role-secret key): `role_changed { userId, role }`,
  `space_ended { spaceId, endedAt }`, plus `track_added { userId, sfuSessionId,
  trackName }` on publish and `track_closed { userId }` on unpublish.
- Client-emitted (already implemented, backend does not re-emit):
  `mute_changed { userId, muted }`, `hand_raised { userId, raised }`.

**Ephemerality**

- Spaces are never recorded. **No egress, capture, or transcription call may
  exist anywhere in the code** — ephemerality is guaranteed by omission, not by
  a flag. Only `started_at` / `ended_at` metadata is persisted; no audio
  artifact may exist in any store after a space ends.

**Constitution compliance (CLAUDE.md)**

- Controllers never touch the Supabase or Cloudflare clients directly beyond
  injected services/repos (§3.2); `Supabase.Client` is passed into repository
  methods, never stored (§3.1); DI against interfaces only (§3.1).
- Write failures throw custom exceptions **inside the repository**; HTTP
  mapping lives in the global exception middleware only (§3.3). This feature
  adds `ForbiddenException → 403` to the middleware's mapping table.
- **Spaces endpoints are exempt from §4.1 caching** (this document is the
  exemption): live-space lookups, participant rosters, and every space mutation
  are real-time state and MUST always be read live. Nothing here is cached, so
  nothing needs invalidation.
- `System.Text.Json` only (§4); every async DB/HTTP call awaited, no
  fire-and-forget (§4) — including broadcasts.
- All list-ish reads (participants of a space) use deterministic ordering
  (e.g. `joined_at` ascending, tie-broken by `id`) (§3.2).

---

## Acceptance

- **Contract**: every route in the table above exists with the exact method,
  path, body envelope, and response shape; DTOs serialize camelCase and
  round-trip against the TypeScript interfaces (`spaceId` not `id`, `isLive`,
  `handRaised`, etc.). Spaces endpoints return plain JSON with no pagination
  header.
- **Authorization matrix** (integration-tested, per §Validation isolation):
  - non-member start → 403; non-member join of a live community space → 403;
  - discussion space start/join by a non-discussion-member → 403;
  - second live space for the same community (and same discussion) → 409;
  - end / approve / demote by a non-host → 403; by the host → 204;
  - publish by a listener → 403, and **no** Cloudflare call is made;
  - publish by host/speaker → succeeds, returns `{ sessionId, trackName,
    answer }`, `track_added` broadcast;
  - subscribe by a participant → `{ sessionId, offer }`; by a non-participant → 403.
- **Role flow**: approve flips the row to `speaker` and broadcasts
  `role_changed`; a subsequent publish from that user succeeds. Demote closes
  the published track, broadcasts `track_closed` + `role_changed`, and a
  subsequent publish from that user → 403.
- **Join** returns the space, the caller's role, the full participant list, and
  the currently-publishing speakers with `sfuSessionId` + `trackName` so the
  joiner can pull each track.
- **Leave/end teardown**: leave closes the participant's SFU sessions and
  broadcasts `track_closed`; end closes all sessions, stamps `ended_at`,
  broadcasts `space_ended`, and the live-lookup endpoints return `null`
  afterward.
- **Reaper**: a participant whose `last_seen_at` exceeds the timeout is reaped
  (SFU sessions closed, `track_closed` broadcast); a space empty past the
  timeout is auto-ended with `space_ended`.
- **Ephemerality**: `started_at` and `ended_at` are recorded; no audio artifact
  exists in any store afterward; the codebase contains no egress/recording/
  transcription call site.
- **Secrets**: Cloudflare app id/secret and the Supabase service-role-secret key come
  from configuration/AWS secrets and never appear in any response, log, or
  client-visible error.
- Deterministic tests, runnable in CI, no shared dev DB (§Validation).

---

## Out of Scope

- Recording, transcription, or audio persistence of any kind.
- Token minting on the client; direct client↔Cloudflare HTTP.
- TURN provisioning (Cloudflare handles it); STUN/ICE config is client-side.
- SFU scaling, cascading, or region routing.
- Video, screen share, spatial audio.
- Discovery feeds beyond the two "live now" lookups above.
- The frontend virtualization work (react-virtuoso) from the sibling frontend
  spec — no backend change; existing paginated endpoints already serve it.
- Client-emitted realtime events (`mute_changed`, `hand_raised`) — already
  implemented on the frontend; the backend neither emits nor validates them.
- Native mobile clients.
