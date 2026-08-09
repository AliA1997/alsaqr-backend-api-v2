# PRD: Video Streaming for Online Events

| | |
|---|---|
| **Status** | Draft — pending resolution of the launch-blocking risks in §6 |
| **Date** | 2026-08-09 |
| **Surface** | Meetup (AlSaqr) |
| **Engineering spec** | [`video-streaming-online-events.md`](./video-streaming-online-events.md) |
| **Reference implementation** | [`audio-spaces.md`](./audio-spaces.md) — frozen, not modified by this work |

---

## 1. Problem Statement

### The user problem

AlSaqr Meetup lets users create and join **online** events, but an online event today
is only a listing: a name, a description, a group, and an attendee roster. There is no
place for the event to actually *happen*. Attendees join an online event and then have
nowhere to go.

The organizer's workaround is to paste a third-party link (Zoom, Meet) into the event
description. That workaround is the real problem, and it costs us three things:

1. **The attendee roster stops meaning anything.** Anyone with the pasted link can
   enter, including people who never joined the event. The `event_attendees` gate we
   already enforce becomes decorative at the exact moment it matters most.
2. **The event leaves our product.** Attention, session time, and every subsequent
   interaction move to a competitor's surface for the duration of the event.
3. **We cannot tell whether an event happened.** `events.times_occurred` and
   `last_occurred_at` are self-reported. We have no signal that anyone showed up.

### Why now

The backend already proves the whole pattern. `specs/audio-spaces.md` ships an
ephemeral, SFU-backed, real-time room with backend-authoritative roles, a presence
channel, and a reaper for dead clients. Video for online events is the same
architecture pointed at a different membership table. The marginal cost is a
repository, a controller, and an SFU adapter — not a new competency.

### What we are explicitly not solving

Not a webinar platform, not a recording/replay product, not in-person event streaming,
and not a chat product (messaging already exists). See §5 and the spec's *Out of Scope*.

---

## 2. Users & User Profiles

Personas below are defined by the **predicate that identifies them in our data**, not
by narrative description. A persona that cannot be evaluated as a query cannot be
enforced as a permission — and every persona here maps directly to an authorization
rule.

| Persona | Identified in data by | Core need | Primary job |
|---|---|---|---|
| **Attendee** *(primary)* | `attendees.user_id = caller` **AND** a matching `event_attendees` row for the event | "The event is starting — let me in, and let me see and hear the others." | Join, see/hear presenters, be visibly present |
| **Organizer** | `event_attendees.is_event_organizer = true` for the event **(see Issue 1 — this is contested)** | "Start the room, control who has the floor, end it when we're done." | Start, promote/demote, end |
| **Presenter** | `video_participants.role = 'presenter'` (or `'host'`) | "I need my camera and mic on without fighting the UI." | Publish camera + mic |
| **Viewer** | `video_participants.role = 'viewer'` | "I want to watch and be counted as here, without being on camera." | Subscribe only; appear in roster |
| **Group Founder** | `groups.founder_id = caller` | "My group's events shouldn't be hijacked." | Escalation/removal authority |

### Anti-personas (explicitly unserved)

- **The non-attendee.** Someone with a link but no `event_attendees` row. Serving them
  is the failure mode we are fixing; they get **403**, including mid-stream.
- **The in-person attendee.** `events.is_online = false` events get **400**. Video is
  gated on the online flag (cost rule C1).
- **The passive lurker at scale.** We are not building a 10,000-viewer broadcast tier.

### Needs validation status — honest accounting

These personas are derived from the **existing data model and the requester's brief**,
not from user research. No interviews, no support-ticket analysis, and no usage data
on how many online events exist or how many attendees they draw were available when
writing this. See Issue 2.

---

## 3. Requirements

Priority definitions: **P0** blocks launch. **P1** ships within the first iteration
after launch. **P2** is explicitly deferred and must not expand P0 scope.

### 3.1 Functional — P0

| ID | Requirement | Rationale / link |
|---|---|---|
| FR-1 | An organizer can start exactly one live video stream for an event where `is_online = true` | Core; DB partial unique index enforces the "one live" invariant |
| FR-2 | A second start attempt while a stream is live returns **409** | Prevents split rooms |
| FR-3 | Start on an in-person event returns **400** before any SFU call | Cost rule C1 |
| FR-4 | Only users attending the event may join; attendance is re-checked on **every** join, including mid-stream | The core problem from §1 |
| FR-5 | A non-attendee join returns **403** and **no token is minted** | A minted token is the credential; refusing after minting is not a refusal |
| FR-6 | On join, the user appears in the roster with `left_at IS NULL` and a `participant_joined` event is broadcast | "Indicate he's in the live" |
| FR-7 | Viewers are receive-only: their token carries `canPublish: false` | Cost rule C4 + permission model |
| FR-8 | The host can promote a viewer → presenter and demote presenter → viewer | Floor control |
| FR-9 | Demotion revokes publish rights **server-side and immediately**, not on the client's next token fetch | See Issue 5 — currently a defect |
| FR-10 | Explicit leave disconnects the user on the SFU and is idempotent | Every client exit path calls it |
| FR-11 | A participant who stops heartbeating past the timeout is auto-disconnected and removed from the roster | "Automatically disconnects the user" |
| FR-12 | The host can end the stream; all participants are disconnected and the room deleted | Teardown |
| FR-13 | A stream left empty past the timeout auto-ends | Cost rule C3 |
| FR-14 | Nothing is ever recorded, captured, or transcribed | Cost rule C10 + §5 compliance posture |

### 3.2 Functional — P1

| ID | Requirement |
|---|---|
| FR-15 | Reconnection grace: a participant who drops and returns within the grace window resumes their prior role rather than re-entering as a viewer *(see Issue 4)* |
| FR-16 | Roster shows camera/mute state per participant |
| FR-17 | Organizer sees a post-event summary: peak concurrency, duration, unique attendees |
| FR-18 | Attendee-facing "stream is live" indicator on the event listing, so joining does not require opening the event |

### 3.3 Functional — P2 (deferred)

Screen sharing · raise-hand → auto-promote · breakout rooms · recording/replay ·
in-person event streaming · reactions · virtual backgrounds · native mobile clients.

### 3.4 Non-functional — P0

| ID | Requirement | Target |
|---|---|---|
| NFR-1 | Join latency (API call → token returned) | p95 < 800 ms |
| NFR-2 | Concurrent participants per stream | ≥ 50 |
| NFR-3 | Concurrent publishers per stream | ≤ 9 (cost rule C5) |
| NFR-4 | Dead-client eviction bound | ≤ 90 s (60 s timeout + 30 s reaper tick) |
| NFR-5 | SFU credentials never reach the browser | Only `wsUrl` + a scoped, short-lived token leave the API |
| NFR-6 | The audio-spaces implementation is not modified | Hard constraint from the requester |

### 3.5 Known requirement gaps

Stated plainly rather than discovered in code review:

- **No max stream duration.** C1–C10 bound *idle* and *empty* cost; an occupied room
  streams unbounded. Addressed in Issue 3.
- **No retention policy** for `video_streams` / `video_participants` rows. Addressed
  in Issue 6.
- **Multi-device join is undefined.** One row per `(video_stream_id, participant_id)`;
  a second device collides on SFU identity.
- **"Organizer" is not unambiguously defined.** Addressed in Issue 1.
- **No requirement covers a groupless online event** (`events.group_id` is nullable,
  and the existing join path throws when it is null).

---

## 4. Success Metrics

Every metric below names the **data source that produces it**. A target without a
source is a wish; the *Instrumented?* column is the honest status today.

### 4.1 Primary metrics

| Metric | Definition | Source | Target (90 days post-launch) | Instrumented? |
|---|---|---|---|---|
| **Stream adoption** | Live streams started ÷ online events that occurred | `video_streams` ÷ `events where is_online` | ≥ 40% | ✅ Derivable |
| **Attendee join rate** | Unique joiners ÷ event attendees, per streamed event | `video_participants` ÷ `event_attendees` | ≥ 50% | ✅ Derivable |
| **Median session duration** | `left_at − joined_at`, per participant | `video_participants` | ≥ 12 min | ✅ Derivable |
| **Peak concurrency per stream** | Max simultaneous `left_at IS NULL` | `video_participants` | ≥ 5 median | ⚠️ Needs periodic sampling — the table stores state, not history |

### 4.2 Guardrail metrics (regressions that block a rollout)

| Metric | Definition | Source | Threshold | Instrumented? |
|---|---|---|---|---|
| **Involuntary disconnect rate** | Reaper-evicted ÷ all departures | Reaper logs vs. `left_at` | < 5% | ❌ Requires distinguishing reaped from explicit leave — see Issue 4 |
| **Join failure rate** | Non-2xx ÷ all join attempts | API telemetry | < 2% | ❌ No metrics pipeline exists |
| **Egress cost per participant-hour** | Cloud egress ÷ participant-hours | Host billing ÷ `video_participants` | ≤ $0.03 | ⚠️ Manual |
| **Unauthorized join attempts** | 403s on join | API telemetry | Monitored, not thresholded | ❌ No metrics pipeline |

### 4.3 Instrumentation gap — must be closed before targets are claimed

`Program.cs` registers `ILogger` and `AddHealthChecks()` and **no metrics or analytics
pipeline**. Four of the eight metrics above cannot be measured on launch day.

**Required P0 addition:** a `left_reason` column (`'explicit' | 'reaped' | 'ended' |
'demoted'`) on `video_participants`. One column converts the entire guardrail set from
"needs a metrics vendor" to "one SQL query," and it is the difference between knowing
our reaper is working and guessing.

### 4.4 Launch gates

Ship to 100% only when: adoption ≥ 20% **and** involuntary disconnect rate < 5%
**and** zero unauthorized joins observed in the authorization audit.

---

## 5. Constraints

### 5.1 Business constraints

| Constraint | Detail |
|---|---|
| **Cost must scale sub-linearly with attendance** | Explicit requester constraint. Drives the self-hosted SFU decision and cost rules C1–C10 |
| **No per-minute vendor metering** | Rules out Cloudflare Calls (used by audio spaces), Twilio, Agora, Daily |
| **Open-source SFU only** | Explicit requester constraint |
| **No new proprietary SDK dependency** | LiveKit is driven over plain HTTPS + HS256 JWT using `System.IdentityModel.Tokens.Jwt`, already referenced by `AlSaqr.Infrastructure` |
| **Audio spaces are frozen** | No edits to `SpacesController.cs` or its supporting files |
| **No dedicated ops team** | The SFU must survive on one node with `--dev`-grade operational effort; multi-region is out of scope |

### 5.2 The cost model (the constraint that shapes the product)

Order-of-magnitude, stated assumptions: 720p simulcast, ~540 kbps for a subscribed
video+audio stream ≈ **0.24 GB per subscriber-hour**; metered cloud egress ≈ $0.09/GB.

| Shape | Streams subscribed per participant | 50 participants × 1 hour | Metered egress cost |
|---|---|---|---|
| **Broadcast** (1 presenter) | 1 | ~50 subscriber-hours ≈ 12 GB | **~$1.08** |
| **Panel** (3 presenters) | 3 | ~150 subscriber-hours ≈ 36 GB | **~$3.24** |
| **Full conversation** (9 presenters) | 9 | ~450 subscriber-hours ≈ 108 GB | **~$9.72** |

Two conclusions that belong in a product document, not a design doc:

1. **Cost scales with publishers × subscribers, not attendance.** The presenter cap is
   the single most important cost lever we have — a 9-presenter room costs ~9× a
   broadcast room with identical attendance.
2. **Host choice dominates SFU choice.** On a bandwidth-included provider
   (~$50/mo, 20 TB), the 9-presenter scenario runs ~185 event-hours inside the flat
   fee. On metered cloud egress the same usage bills ~$1,800. Choosing LiveKit saves
   the metering; choosing where to run it saves the bandwidth.

### 5.3 Compliance and legal

| Area | Position |
|---|---|
| **Personal data** | Live video/audio of identifiable people is personal data under GDPR. Our posture is **data minimization by architecture**: no egress, capture, or transcription code path exists, so no media is ever at rest — the strongest possible answer to a subject-access or erasure request |
| **Retention** | ⚠️ **Gap.** `video_streams` and `video_participants` rows are personal data (who was in a call, when, for how long) and have **no stated retention period or purge job**. See Issue 6 |
| **Erasure / Art. 17** | User deletion cascades via `ON DELETE CASCADE` on `participant_id` → `users(id)`. ✅ Satisfied |
| **Consent & notice** | Joining is an affirmative act, and the roster makes presence visible to all. Camera/mic activation is a browser-level permission prompt. Users must be told **no recording occurs** — this is a feature, and silence about it invites the assumption that we do record |
| **Participant-side recording** | We cannot prevent a participant screen-recording. Requires a Terms/community-guidelines line; it is a policy control, not a technical one |
| **Minors** | ⚠️ **Open.** Meetup events may include under-18 attendees; `users.date_of_birth` exists but no age gate is specified for video. Needs a legal decision before launch |
| **Data residency** | A single self-hosted node means all media transits one region. If EU attendees are material, node placement is a compliance decision, not a latency one |

### 5.4 Technical constraints inherited from the platform

- **`ExceptionHandlingMiddleware` is disabled** ([`Program.cs:189`](../AlSaqr.API/Program.cs#L189)).
  Until re-enabled, every 400/403/404/409 in §3.1 returns **500**. Launch-blocking.
- **`UserCacheService` resolves the caller from a single global cache slot**
  ([`UserCacheService.cs:57`](../AlSaqr.Infrastructure/UserCacheService.cs#L57)),
  registered as a process-wide singleton. Under concurrency it returns whoever logged
  in most recently. Launch-blocking — see Issue 5.
- Supabase PostgREST is the only data path; repositories take `Supabase.Client` per
  method (CLAUDE.md §3.1).

---

## 6. Identifying the Issues

Each issue below states **what the problem is**, **why it matters**, **specific
feedback**, and a **corrected version** of the affected section.

---

### Issue 1 — The "Organizer" persona is not implementable *(§2)*

**What's the problem.** §2 defines Organizer as `event_attendees.is_event_organizer =
true`, but the codebase contains a second, competing authority: `RemoveEventAttendee`
gates on `groups.founder_id`
([`EventAttendeeRepository.cs:155`](../AlSaqr.Data/Repositories/Meetup/EventAttendeeRepository.cs#L155)).
Two definitions of "who runs this event" exist in the same product.

**Why it matters.** This persona is not a description — it is a **permission**. FR-1,
FR-8, and FR-12 all resolve to it. Shipping the ambiguity means either a founder cannot
start their own group's event, or a flagged attendee can seize a room the founder
cannot end. The bug surfaces in production, during a live event, in front of attendees.

**Specific feedback.** Personas that back permissions must be single-valued and
expressed as a predicate. Where two authorities genuinely exist, model them as
**distinct personas with distinct rights**, not as one fuzzy role.

**Corrected version:**

> | Persona | Predicate | Rights |
> |---|---|---|
> | **Event Organizer** | `event_attendees.is_event_organizer = true` for this event | Start, promote/demote, end |
> | **Group Founder** | `groups.founder_id = caller` on the event's host group | All organizer rights, **plus** the ability to end a stream started by anyone else (escalation path for an absent or abusive organizer) |
>
> Resolution order is founder-first. An event with `group_id IS NULL` has **no
> organizer and cannot host video** — the request returns `400`, consistent with the
> existing attendance path, which already refuses groupless events.

---

### Issue 2 — Personas assert needs that were never validated *(§2)*

**What's the problem.** Every persona was derived from the data model and the
requester's brief. Not one came from a user. The PRD reads as though the needs are
established.

**Why it matters.** The riskiest assumption is not "will this work" — it is **"do
online events on this platform have enough attendees to make many-to-many video the
right shape."** A room built for nine simultaneous presenters costs ~9× a broadcast
room (§5.2). If the median online event draws four attendees, we have overbuilt and
overspent; if it draws sixty, the 50-participant NFR-2 target is already too low. We
do not know which, and the answer is already sitting in the `events` and
`event_attendees` tables.

**Specific feedback.** Do not run interviews to unblock this. Run **one query**, before
implementation starts.

**Corrected version — add to §2:**

> **Assumption validation (P0, pre-implementation).** Before a line of code is
> written, produce the distribution of `event_attendees` counts for events with
> `is_online = true` over the last 180 days (since ~2026-02-10):
>
> ```sql
> select
>   count(*)                                             as online_events,
>   percentile_cont(0.5) within group (order by n)       as median_attendees,
>   percentile_cont(0.9) within group (order by n)       as p90_attendees,
>   max(n)                                               as max_attendees
> from (
>   select e.id, count(ea.id) as n
>   from "alsaqr-2026".events e
>   left join "alsaqr-2026".event_attendees ea on ea.event_id = e.id
>   where e.is_online = true
>     and e.created_at > now() - interval '180 days'
>   group by e.id
> ) s;
> ```
>
> **Decision rule:** median ≤ 5 → ship broadcast-shaped (cap presenters at 3, defer
> FR-8) and cut projected egress by two-thirds. Median ≥ 15 → the 9-presenter cap and
> NFR-2 stand. Zero online events in 180 days → **stop**; this is a demand problem,
> not a video problem, and no amount of SFU work fixes it.

---

### Issue 3 — "Cost effective" is asserted, never bounded *(§3, §5)*

**What's the problem.** Cost-effectiveness is a headline requirement, and §5.2 models
it well — but **no requirement makes it falsifiable**. There is no budget, no cap on
stream duration, and no defined behavior when spend exceeds expectation. Ten cost
rules constrain the *mechanism* while nothing constrains the *outcome*.

**Why it matters.** Unbounded occupied streams are the realistic runaway: rules C3 and
C9 kill *idle* and *empty* rooms, but forty people who leave a tab open overnight are
neither idle nor empty. That is ~$9.72/hour of metered egress against a requirement
whose only stated bound is "cost effective." Nobody can tell whether we succeeded.

**Specific feedback.** Convert the constraint into a numeric requirement with a
mechanism and a defined breach behavior. Add C11 and the corresponding FR.

**Corrected version — add to §3.4 and §5.1:**

> | ID | Requirement | Target |
> |---|---|---|
> | **NFR-6** | Blended infrastructure cost per participant-hour | **≤ $0.03**, measured monthly against host billing ÷ `video_participants` duration |
> | **NFR-7** | Maximum stream duration | **4 hours**, after which the stream auto-ends with a `stream_ended` broadcast and a 10-minute warning event |
> | **NFR-8** | Monthly video infrastructure spend | **≤ $250**; breach triggers review, not an automatic cut-off — cutting live events mid-stream to save $50 is worse than the $50 |
>
> **Cost rule C11 (binding):** every stream carries a hard `ended_at` ceiling of
> `started_at + 4h`, enforced by the reaper independently of participant activity.
> This is the only guard against an occupied-but-abandoned room, which C3 and C9 by
> construction cannot catch.

---

### Issue 4 — "Automatically disconnects" is written as a feature; users will experience it as a bug *(§1, §3)*

**What's the problem.** The success criterion, inherited verbatim from the brief,
treats auto-disconnect as a win. It is a win for **one** case — a dead client whose
browser is gone. For the other case, a live human on hotel wifi that dropped for 70
seconds, the identical mechanism silently ejects them from the event, and on return
they re-enter as a **viewer with their presenter role stripped**.

**Why it matters.** This is the difference between infrastructure hygiene and a
product defect, and the PRD currently cannot distinguish them — not in the
requirements, and not in the metrics, since §4.2's involuntary-disconnect guardrail
has no data source. We would ship a mechanism whose most common user-visible failure
we have no way to observe. A presenter mid-sentence losing the floor to a network
blip is the single worst moment this feature can produce.

**Specific feedback.** Split the outcome in two: reaping a dead client is invisible and
correct; disconnecting a live user is a failure with a recovery path. Then instrument
the difference so the guardrail is real.

**Corrected version — replaces FR-11, promotes FR-15 to P0:**

> | ID | Requirement | Priority |
> |---|---|---|
> | **FR-11** | A participant who stops heartbeating past **60 s** is disconnected on the SFU and marked `left_at` with `left_reason = 'reaped'` | P0 |
> | **FR-11a** | A reaped participant who rejoins within a **5-minute grace window** resumes their **prior role** (presenter stays presenter) rather than re-entering as a viewer | **P0** |
> | **FR-11b** | Every departure records `left_reason ∈ ('explicit','reaped','ended','demoted')` | **P0** |
> | **FR-11c** | The client surfaces "Reconnecting…" on heartbeat failure and retries automatically before the user is told anything is wrong | P1 |
>
> **Metric correction (§4.2):** *Involuntary disconnect rate* =
> `count(left_reason='reaped') ÷ count(*)`, from `video_participants` — no metrics
> vendor required. Threshold < 5%. Above 10%, the timeout is wrong, not the network.

---

### Issue 5 — Two launch-blocking platform defects are listed as constraints, not as work *(§5.4)*

**What's the problem.** §5.4 records the disabled exception middleware and the
single-slot `UserCacheService` as inherited constraints — accurate, and passive. Nobody
owns them, they appear in no requirement table, and the plan of record therefore ships
without them being fixed. A third defect (token replay, spec §F1) is not in this PRD at
all.

**Why it matters.** Each independently breaks a P0 requirement:

- **Disabled middleware** → FR-2, FR-3, FR-5 return **500** instead of 409/400/403.
  Every authorization requirement in §3.1 is unverifiable.
- **Single-slot user cache** → the caller cannot be identified under concurrent load,
  so FR-4/FR-5 can mint **user A's token for user B's request**. That is not a stale
  read; it is the backend signing a credential for the wrong person.
- **Token replay** (room name stable across streams, 10-minute TTL) → FR-9 is
  false as designed: a demoted presenter replays their old publish token and keeps the
  floor.

All three defeat the one sentence this feature exists to guarantee: *only users in the
event can join the video call*.

**Specific feedback.** Anything that must be true at launch belongs in the requirements
table with an ID and an owner. "Constraint" is where work goes to be forgotten.

**Corrected version — new §3.6:**

> ### 3.6 Launch-blocking dependencies (P0)
>
> | ID | Dependency | Blocks | Fix |
> |---|---|---|---|
> | **DEP-1** | Re-enable `app.UseMiddleware<ExceptionHandlingMiddleware>()` | FR-2, FR-3, FR-5 | One line; already written and tested. Also silently affects audio spaces today |
> | **DEP-2** | Resolve caller identity from the validated JWT `sub` claim; use `UserCacheService` only as a profile cache **keyed by user id** | FR-4, FR-5 | Contained; also fixes the same latent flaw across every authenticated controller |
> | **DEP-3** | Stream-scoped room names (`stream-{videoStreamId}`), demote via LiveKit `UpdateParticipant` (server-side revocation), token TTL 60–120 s | FR-9 | Spec change; see engineering spec §F1 |
>
> None of these are optional and none are "hardening." Without DEP-1 and DEP-2 the
> authorization matrix cannot even be tested, and this feature is an authorization
> feature.

---

### Issue 6 — Compliance covers media but not metadata *(§5.3)*

**What's the problem.** The no-recording posture is genuinely strong and correctly
argued: no media at rest, so subject-access and erasure requests are trivially
satisfied. But `video_participants` retains, indefinitely, **who was in a call with
whom, when, and for how long** — a social graph with timestamps — and the PRD's
compliance section treats "we don't record" as if it settled the matter.

**Why it matters.** Attendance metadata is personal data under GDPR exactly as media
is, and it is arguably more sensitive in aggregate: it reveals affiliation and
association patterns across religious and community events, which is special-category
territory under Art. 9. We would be in the odd position of aggressively minimizing the
video while quietly accumulating the graph forever. "We never record" is also a claim
users will read as "nothing is kept," and nothing in the current design makes that
true.

**Specific feedback.** Ephemerality must extend to metadata, with a retention period
and a purge job — otherwise the architectural claim in §5.3 is only half true.

**Corrected version — replaces the Retention row in §5.3:**

> | Area | Position |
> |---|---|
> | **Retention** | `video_participants` rows are purged **90 days** after `left_at`. `video_streams` rows are aggregated to a per-event summary (peak concurrency, duration, unique attendees — feeding FR-17) and the underlying rows purged on the same schedule. Implemented as a scheduled job alongside the reaper, covered by **FR-19 (P0)** |
> | **Erasure** | `ON DELETE CASCADE` on `participant_id → users(id)` removes all participation history on account deletion. ✅ |
> | **Disclosure** | The join UI states plainly: *"This call is not recorded. We keep a record of who joined and for how long for 90 days."* Accurate beats reassuring — and the second sentence is what makes the first one credible |
>
> **Open decision, needs legal before launch:** whether under-18 attendees
> (`users.date_of_birth` is available) may join video on events open to adults. This
> is a policy question, and it is the only item in this PRD that engineering cannot
> resolve on its own.

---

## Summary of changes this review produces

| # | Change | Priority |
|---|---|---|
| 1 | Split Organizer into Event Organizer + Group Founder with explicit predicates | P0 |
| 2 | Run the attendance-distribution query and pick the room shape from the result | **P0, pre-implementation** |
| 3 | Add NFR-6/7/8 and cost rule C11 (4-hour ceiling) | P0 |
| 4 | Add `left_reason` column; add 5-minute role-preserving reconnect grace | P0 |
| 5 | Promote the three platform defects to §3.6 with IDs and owners | P0 |
| 6 | 90-day metadata retention + purge job (FR-19); resolve the minors question | P0 |

**Recommendation:** do not begin implementation until change #2 is answered. It is one
query, it takes minutes, and it determines whether the presenter cap, NFR-2, and
roughly two-thirds of the projected egress cost are correctly sized. Everything else on
this list is work; that one is a decision, and it is cheaper to make it now than to
rebuild the room shape after launch.
