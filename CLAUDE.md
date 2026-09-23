# AlSaqr — Backend Project Constitution

Rules for how code is written in this backend, not a description of the current state.
**MUST** / **MUST NOT** / **SHOULD** / **MAY** carry their RFC-2119 meanings. Anything a rule
does not permit is out of scope and needs an explicit decision first.

---

## 1. Tech Stack

.NET 8.0 · Microsoft.EntityFrameworkCore 8.0.20 · Supabase 1.0.5 · Neo4j.Driver 5.28.3 ·
NewsAPI 0.7.0 · Newtonsoft.Json 13.0.4 · Microsoft.Extensions.Caching.\* (per lock file)

All business-data querying goes through the Supabase PostgREST client (§3.2) — never EF Core.
EF Core itself is still live, though: `AppDbContext` (`AlSaqr.Data/DbContext.cs`) backs ASP.NET
Identity via `AddIdentityApiEndpoints<User>().AddEntityFrameworkStores<AppDbContext>()` in
`Program.cs`, on an **in-memory** provider. Don't remove the package — see §8 before touching
that provider choice.

Neo4j.Driver is referenced and `AlSaqr.Data/Helpers/Neo4jHelper.cs` exists, but nothing in the
solution instantiates a driver or calls the helper — it's unused scaffolding.

`System.Text.Json` is the standard (§4), but `Newtonsoft.Json` already ships in ~20 files across
three projects. Don't add more; don't drive-by-remove what's there either — see §8.

No test project exists in the solution yet. §6's testing rules are the target for new test
infrastructure, not a description of something already in place.

---

## 2. Project Structure

Each project has one responsibility and MUST NOT take on another's.

| Project | Owns | MUST NOT contain |
|---|---|---|
| **AlSaqr.API** | HTTP controllers, request/response mapping | data access, caching, business rules |
| **AlSaqr.Domain** | DTOs only, organized by feature area (`Common`, `Meetup`, `SocialMedia`, `Yumna`, `Zook`, `Utils`) | entities |
| **AlSaqr.Infrastructure** | cross-cutting services, primarily caching (`SocialMediaCacheService`, `UserCacheService`) | |
| **AlSaqr.Data** | entities mapped to Supabase tables, plus repositories | |

These four are the whole solution. A fifth project, `AlSaqr.Services`, was an unused placeholder
and has been removed — do not reintroduce it. New code belongs in one of the four above; a new
project is an architecture decision, not a convenience.

"Domain" here means **DTOs**, not domain entities — entities live in `AlSaqr.Data`. This
deviates from the usual convention deliberately; preserve it and do not move entities.

---

## 3. Architectural Principles

### 3.1 Dependency Injection

- Dependencies MUST arrive by **constructor injection**, registered in the DI container.
  Property injection MUST NOT be used.
- Repositories and services MUST be registered against an interface (`IUserRepository`,
  `ISocialMediaCacheService`), never a concrete type.
- `Supabase.Client` MUST be passed into repository **methods as a parameter**, never stored as
  repository state.

```csharp
public Task<PaginatedResult<ProfilePostDto>> GetProfilePostsAsync(
    Supabase.Client client, Guid userId, int currentPage, int itemsPerPage, string? searchTerm);
```

### 3.2 Repository Pattern

- All PostgreSQL access MUST go through a repository using the Supabase PostgREST client.
  Controllers MUST NOT touch the Supabase client directly.
- Every entity MUST carry `[Table("name")]`. The schema is supplied **once**, via
  `SupabaseOptions.Schema` in `Program.cs` — it MUST NOT be repeated on the `[Table]` attribute
  or inside query paths (`Filter("alsaqr-2026.author_id", …)` is wrong). Every entity in the
  solution already follows this; adding `Schema =` to one would double it for that table alone.
- `json` aggregate columns MUST map to `string?`. `jsonb` follows the same rule unless
  deserialized into a typed model (several views already do this for comment/liker/reposter
  aggregates — that's the sanctioned exception, not a violation).
- `currentPage`, `itemsPerPage`, and `searchTerm` MUST be accepted as query params on the
  endpoint and passed through to the repository.
- Paginated reads MUST return `PaginatedResult<T>` (`AlSaqr.Domain.Utils.Common`). There is no
  shared paging helper yet — `skip`/`Range` and the `Pagination` object are built inline per
  repository method, as below. Don't invent a name for a utility that doesn't exist; if you
  factor the duplication out, that's in the spirit of §4's DRY rule, not a deviation from it.
  Counts MUST come from a PostgreSQL RPC through `SupabaseHelper.CallFunction` — never a
  client-side count of a fetched page. `CallFunction` returns the raw `string` response; parse it
  (`long.Parse`) rather than assuming a generic overload.
- **Ordering MUST be deterministic**: an explicit `ORDER BY` on a unique or tie-broken key.
  Non-deterministic ordering is a defect, not a style issue — even though most existing
  repositories today order by a single timestamp column with no tie-breaker. Bring a query into
  compliance when you're already touching it; don't sweep the solution for this alone.
- For null filtering, use the explicit operator — `Filter("ended_at", Operator.Is, "null")` and
  its negated form — rather than comparing against the text `"null"`.

The canonical paged read (matches `PostRepository.GetBookmarkedPosts`):

```csharp
var skip = (currentPage - 1) * itemsPerPage;

var countResult = await SupabaseHelper.CallFunction(
    client, "count_profile_posts", new Dictionary<string, object> { { "p_author_id", userId } });
var totalItems = countResult != null ? long.Parse(countResult) : 0;

var page = await client
    .From<Post>()
    .Filter("user_id", Operator.Equals, userId.ToString())
    .Order("created_at", Ordering.Descending)
    .Range(skip, skip + itemsPerPage - 1)
    .Get(ct);

return new PaginatedResult<PostDto>(
    page.Models.Select(p => new PostDto(p)).ToList(),
    new Pagination
    {
        ItemsPerPage = itemsPerPage,
        CurrentPage = currentPage,
        TotalItems = (int)totalItems,
        TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage),
    });
```

Writes follow fetch → mutate → `Upsert` with
`new QueryOptions { Returning = QueryOptions.ReturnType.Representation }`, returning the model
from the response.

### 3.3 Exceptions

- Custom exceptions MUST be thrown **inside repositories** for failed POST / PUT / PATCH /
  DELETE. Controllers MUST NOT throw them.
- A **single global middleware** (`AlSaqr.API/Middleware/ExceptionHandlingMiddleware.cs`) MUST
  catch them and emit RFC 7807 `ProblemDetails`. It is the only place exceptions become HTTP
  responses; per-controller try/catch for HTTP mapping MUST NOT be used.
- That middleware holds the one authoritative exception → status mapping:
  `NotFoundException` → 404, `ValidationException` → 400, `ForbiddenException` → 403,
  `ConflictException` → 409, everything else → 500.
- The middleware is currently **not registered** in `Program.cs` (the `UseMiddleware` call is
  commented out) — see §8 before re-enabling it.

---

## 4. Code Standards

- **DRY** — shared logic goes into utilities or base classes, never copy-paste.
- `System.Text.Json` throughout. `Newtonsoft.Json` MUST NOT be introduced in new code (see §1
  and §8 for what already exists).
- Every async DB call MUST be awaited. Fire-and-forget (`_ = SomethingAsync()`) is forbidden —
  an unobserved cache invalidation may simply not run.

### 4.1 Caching Contract

Binding, because stale cache is the likeliest source of user-visible bugs in a social app.

- GET endpoints returning user-scoped data MUST cache under a key containing the user id and the
  resource, e.g. `initialPosts_{userId}` (see the `*CacheService` partials under
  `AlSaqr.Infrastructure/SocialMediaCacheService/` for the live naming convention — underscore-
  joined prefix + id, not colon-delimited, and one entry per user holding that resource's
  *initial* page load, not one entry per page).
- Every entry MUST have an explicit TTL via `MemoryCacheEntryOptions` (sliding + absolute) — there
  is no universal default; each resource picks its own (existing values range from 5 minutes to
  2 hours for social data, up to 14 days for the logged-in session in `UserCacheService`, which
  is deliberately long-lived and not a bug to "fix" down to a short TTL). A new cache entry MUST
  still pick options explicitly rather than calling `_cache.Set` with none.
- **Writes MUST invalidate reads, in the same operation:**
  - new post → evict `initialPosts_{authorId}` and affected feed keys
  - follow / unfollow → evict follower *and* following count keys for **both** users
  - like / repost / comment → evict the post's aggregate-count key
- The logged-in user MUST be written and read through `UserCacheService` — never re-fetched ad
  hoc via the repository.
- **Never cached, always read live:** direct messages, community-discussion message threads, and
  any real-time message-history endpoint. `MessageCacheService` and
  `CommunityDiscussionCacheService` currently cache both of these — that's a standing violation
  of this rule, not sanctioned prior art. See §8.

---

## 5. Scope

Three apps share this backend, plus two cross-cutting features not owned by any one of them.
These boundaries are authoritative.

**SocialMedia** — view profiles; follow/unfollow; profile info, posts, and media; like, repost,
and comment on posts; join communities and community discussions; direct messages and
community-discussion messages; ephemeral audio Spaces (Twitter-Spaces-style, Cloudflare Calls
SFU); lists that can save posts, comments, users, communities, discussions, and discussion
messages. Nearly all of it requires a logged-in user, read from cache per §4.1.

**Meetup** — join a group or event; direct-message local guides; events online or in person;
create groups and events for a user.

**Zook** — products across multiple categories; create, update, and delete products for a user.

**Yumna** — subscription-gated AI assistant with a per-user daily request limit.

---

## 6. Workflow (SDD)

Specification → Technical Planning → Task Breakdown → Implementation → Validation.

**Task breakdown** produces, per feature: entity classes mapped from the provided table
definition; an `I{Name}Repository` interface plus implementation taking `Supabase.Client` as a
method parameter; and GET methods cached by user id per §4.1 unless exempted above.

**Implementation** is functional and under refactoring. A refactor MUST NOT weaken any MUST
rule — refactors bring code into compliance, never out of it.

**Validation:**

- Tests MUST NOT run against the shared dev database. Each run MUST get isolated, disposable
  state — Testcontainers with a throwaway PostgreSQL, or a dedicated test schema with per-test
  transaction rollback.
- Tests MUST be deterministic and runnable in CI with no manual setup and no leftover data.
- Tests SHOULD assert ordering, pagination boundaries, and cache invalidation — historically the
  defect-prone areas.

---

## 7. Definition of Done

Every applicable MUST rule satisfied, deterministic tests covering the new behavior, no dead
code left behind, and no caching introduced without a matching invalidation rule.

---

## 8. Known Gaps — Verify Before Touching

These look like defects an agentic session would reflexively "fix" in passing. Each is either a
deliberate deviation, a genuine standing bug whose fix has a blast radius, or ambiguous enough
that the call belongs to a person, not to whoever happens to be nearby:

- **Exception middleware is unregistered.** `Program.cs` comments out
  `app.UseMiddleware<ExceptionHandlingMiddleware>()`, even though the class itself exists,
  is correctly implemented, and cites §3.3 in its own doc comment. As shipped, unhandled
  repository exceptions are not currently converted to `ProblemDetails` anywhere. Re-enabling it
  changes the response shape and status code of every endpoint in the API at once — ask first,
  and confirm nothing downstream depends on the current (uncaught) behavior before flipping it on.
- **ASP.NET Identity runs on `UseInMemoryDatabase`.** Every registered user is lost on app
  restart. This looks broken and may well be a placeholder that was never swapped for a real
  provider — but changing it is a data-persistence decision (and possibly a migration), not a
  drive-by fix. Raise it rather than swapping the provider unilaterally.
- **Message threads and community-discussion messages are cached**, contradicting §4.1's
  "never cached, always read live" rule — see `MessageCacheService.cs` and
  `CommunityDiscussionCacheService.cs`. Don't extend that caching further, and don't rip it out
  solo either: whether the rule or the code is wrong is a staleness-risk call for the user to
  make, not something to resolve mid-unrelated-task.
- **`Newtonsoft.Json` is already in ~20 files.** §4's "MUST NOT be introduced" governs new code.
  It is not license to migrate existing files to `System.Text.Json` as a side effect of touching
  a nearby line — that's a separate, deliberate cleanup.
- **Most repositories order by a single timestamp with no tie-breaker**, despite §3.2's
  determinism rule. Fix it where you're already working in a query; don't batch-edit every
  `ORDER BY` in the solution as a standalone pass.
