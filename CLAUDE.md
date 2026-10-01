# AlSaqr — Backend Project Constitution

Rules for how code is written in this backend, not a description of a finished state.
**MUST** / **MUST NOT** / **SHOULD** / **MAY** carry RFC-2119 meanings. Anything a rule does not
permit is out of scope and needs an explicit decision first.

## 1. What this is

.NET 8 API backend for three apps — SocialMedia, Meetup, Zook — plus Yumna, sharing one
solution. Business data lives in Supabase Postgres and MUST be queried only through the Supabase
PostgREST client; EF Core is not the data-access path (§2).

## 2. Stack — non-default facts

- `AlSaqr.API` / `.Data` / `.Domain` / `.Infrastructure` are the whole solution. A fifth project,
  `AlSaqr.Services`, was an unused placeholder and was removed — don't reintroduce one without a
  reason; a new project is an architecture decision, not a convenience.
- "Domain" here means **DTOs**, organized by feature area (`Common`, `Meetup`, `SocialMedia`,
  `Yumna`, `Zook`, `Utils`) — entities live in `AlSaqr.Data`, not Domain. Deliberate deviation
  from the usual convention; preserve it.
- EF Core is live, but only backs ASP.NET Identity (`AppDbContext`,
  `AddIdentityApiEndpoints<User>().AddEntityFrameworkStores<AppDbContext>()` in `Program.cs`) on
  `UseInMemoryDatabase("AlSaqr")` — see §4.
- `Neo4j.Driver` is referenced and `AlSaqr.Data/Helpers/Neo4jHelper.cs` exists, but nothing in
  the solution instantiates a driver or calls the helper — unused scaffolding.
- `Newtonsoft.Json` already ships in ~20 files across three projects; `System.Text.Json` (§3) is
  required for new code only.
- No test project exists yet — §7 is the target for new test infrastructure, not a description
  of something already in place.

## 3. Invariants

**Dependency injection & repositories** — constructor injection only, no property injection.
Repositories/services register against an interface, never a concrete type. `Supabase.Client` is
passed into repository **methods** as a parameter, never stored as instance state, so a
repository can't hold onto a stale or wrong-tenant client. All Postgres access goes through a
repository via Supabase PostgREST; controllers never touch the Supabase client directly.

**Repository conventions:**
- Every entity carries `[Table("name")]` with no `Schema =`. The schema is supplied once, via
  `SupabaseOptions.Schema` in `Program.cs` (from `Supabase:Schema` config) — repeating it on the
  attribute or inside a `Filter` path double-applies it.
- `json`/`jsonb` aggregate columns map to `string?`, unless deserialized into a typed model
  (several views already do this for comment/liker/reposter aggregates — sanctioned, not a
  violation).
- `currentPage`, `itemsPerPage`, `searchTerm` are accepted as query params and passed through to
  the repository. Paginated reads return `PaginatedResult<T>` (`AlSaqr.Domain.Utils.Common`).
  There's no shared paging helper — `skip`/`Range` and the `Pagination` object are built inline
  per method (see `PostRepository.GetBookmarkedPosts`). Counts come from a Postgres RPC via
  `SupabaseHelper.CallFunction`, never a client-side count of a fetched page; it returns a raw
  `string` — parse with `long.Parse`.
- Ordering MUST be deterministic: an explicit `ORDER BY` on a unique or tie-broken key. Most
  existing repositories order by a single timestamp with no tie-breaker — bring a query into
  compliance when you're already touching it, don't sweep the solution for this alone.
- Null filtering uses the explicit operator (`Filter("ended_at", Operator.Is, "null")`, and its
  negated form), never the text `"null"`.
- Writes: fetch → mutate → `Upsert` with `QueryOptions.ReturnType.Representation`, returning the
  model from the response.

**Exceptions** — custom exceptions are thrown inside repositories for failed
POST/PUT/PATCH/DELETE, never in controllers. `ExceptionHandlingMiddleware` is meant to be the
one authoritative exception → status mapping (`NotFoundException`→404, `ValidationException`
→400, `ForbiddenException`→403, `ConflictException`→409, else→500) and the only place exceptions
become HTTP responses — but it is currently unregistered (§4).

**Code standards** — DRY: shared logic goes into utilities or base classes. Every async DB call
MUST be awaited; fire-and-forget (`_ = SomethingAsync()`) is forbidden because an unobserved
cache invalidation may simply not run.

**Caching** (binding — stale cache is the likeliest source of user-visible bugs here):
- GET endpoints returning user-scoped data cache under an underscore-joined `resource_{userId}`
  key (e.g. `initialPosts_{userId}`) — one entry per user holding that resource's *initial* page
  load, not one entry per page (see `AlSaqr.Infrastructure/SocialMediaCacheService/*`).
- Every entry sets an explicit TTL via `MemoryCacheEntryOptions` (sliding + absolute). There's no
  universal default — existing values run 5 min–2 hr for social data, 14 days for the logged-in
  session in `UserCacheService` (deliberately long-lived, not a bug).
- Writes MUST invalidate reads in the same operation. Today only the new-post case actually does
  this (evicts `initialPosts_{authorId}` and affected feed keys) — follow/unfollow count keys and
  like/repost/comment aggregate-count keys are **not** evicted anywhere in the repo despite the
  rule; don't assume that invalidation exists when building on top of it.
- The logged-in user is written/read only through `UserCacheService`, never re-fetched ad hoc.
- Never cached, always read live: direct messages and any real-time message-history endpoint.

## 4. Do not "fix" these — ask first

Each is a deliberate deviation or a fix whose blast radius makes it a call for a person, not
whoever's nearby. The same applies to adding/removing a package, reintroducing a fifth project,
renaming or removing a documented Supabase table/RPC/view, or changing the caching contract's
TTLs, key format, or `SupabaseOptions.Schema`.

- **Exception middleware is unregistered** — `Program.cs` comments out
  `app.UseMiddleware<ExceptionHandlingMiddleware>()`. In its place, ~12 controllers (e.g.
  `Zook/ProductsController.cs`) catch exceptions locally and `return StatusCode(...)` directly —
  itself a standing violation of §3's "controllers never map exceptions to HTTP" rule. The two
  are the same gap: re-enabling the middleware changes every endpoint's response shape at once
  and should come paired with removing the controller-level catches, not alongside them.
- **ASP.NET Identity runs on `UseInMemoryDatabase`** — every registered user is lost on restart.
  Looks broken and may be an unswapped placeholder, but changing it is a data-persistence (and
  possibly migration) decision.
- **`CommunityDiscussionCacheService` caches community-discussion messages** on a live read path,
  contradicting the "never cached" rule above. `MessageCacheService` has matching set/get methods
  for DM threads, but no controller currently calls them, so direct messages aren't actually
  cached today despite the service existing — don't extend either, and don't rip out the live
  community-discussion violation solo either.
- **`SocialMediaCacheService.SetInitialProductCategories` calls `_cache.Set` with no
  `MemoryCacheEntryOptions`** — an unbounded-lifetime entry, contradicting the explicit-TTL rule.
- **`Newtonsoft.Json` is already in ~20 files.** §3's "MUST NOT be introduced" governs new code;
  it isn't license to migrate existing files to `System.Text.Json` as a side effect of touching a
  nearby line.

## 5. Scope

**SocialMedia** — profiles, follow/unfollow, posts/media, likes/reposts/comments, communities and
discussions, direct messages, ephemeral audio Spaces (Cloudflare Calls SFU), lists (posts, users,
communities, discussions, discussion messages — **not** comments: `ListItemRepository` has no
comment type and throws for one). Nearly all of it requires a logged-in user, cached per §3.

**Meetup** — join a group/event; events online or in person; create groups/events for a user.
No local-guide-specific messaging exists — `LocalGuidesController` has no message endpoint;
guides use the same generic SocialMedia messaging as everyone else.

**Zook** — products across categories; create/update/delete products for a user.

**Yumna** — AI assistant with a per-user daily request limit. Not currently subscription-gated:
`YumnaController` falls back to a hardcoded default (`DefaultDailyRequestLimit = 30`) for any
logged-in user with no subscription, rather than blocking them.

## 6. Workflow (SDD)

Specification → Technical Planning → Task Breakdown → Implementation → Validation. Task
breakdown produces, per feature: entity classes mapped from the provided table definition; an
`I{Name}Repository` + implementation taking `Supabase.Client` as a method parameter; GET methods
cached by user id per §3 unless exempted above.

Implementation is functional and under refactoring. A refactor MUST NOT weaken any MUST rule —
refactors bring code into compliance, never out of it.

## 7. Verification

No test project exists yet, so there is no command to run. When one is added: tests MUST NOT run
against the shared dev database (Testcontainers with a throwaway Postgres, or a dedicated schema
with per-test transaction rollback), MUST be deterministic with no manual setup or leftover data,
and SHOULD assert ordering, pagination boundaries, and cache invalidation — historically the
defect-prone areas.

## 8. Definition of Done

Every applicable MUST rule satisfied, deterministic tests covering the new behavior, no dead
code left behind, and no caching introduced without a matching invalidation rule.
