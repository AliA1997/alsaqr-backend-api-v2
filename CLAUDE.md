# AlSaqr — Backend Project Constitution

Rules for how code is written in this backend, not a description of the current state.
**MUST** / **MUST NOT** / **SHOULD** / **MAY** carry their RFC-2119 meanings. Anything a rule
does not permit is out of scope and needs an explicit decision first.

---

## 1. Tech Stack

.NET 8.0 · Microsoft.Extensions.Hosting.Abstractions 8.0.1 · Microsoft.EntityFrameworkCore
8.0.20 · Microsoft.Extensions.Caching.\* (per lock file) · NewsAPI 0.70 · Supabase 1.0.5

EF Core is referenced but **not** the data-access path — all querying goes through the Supabase
PostgREST client (§3.2). The package SHOULD be removed rather than left implying an ORM that
isn't there.

---

## 2. Project Structure

Each project has one responsibility and MUST NOT take on another's.

| Project | Owns | MUST NOT contain |
|---|---|---|
| **AlSaqr.API** | HTTP controllers, request/response mapping | data access, caching, business rules |
| **AlSaqr.Domain** | DTOs only — Requests / Responses / Shared | entities |
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
- Every entity MUST carry `[Table("name", Schema = "alsaqr-2026")]`. The schema MUST NOT be
  repeated inside query paths (`Filter("alsaqr-2026.author_id", …)` is wrong).
- `json` aggregate columns MUST map to `string?`. `jsonb` follows the same rule unless
  deserialized into a typed model.
- `currentPage`, `itemsPerPage`, and `searchTerm` MUST be accepted as query params on the
  endpoint and passed through to the repository.
- Paginated reads MUST return `PaginatedResult<T>` via `SocialMediaQueryUtility.GetPagedAsync`.
  Counts MUST come from a PostgreSQL RPC through `SupabaseHelper.CallFunction` — never a
  client-side count of a fetched page.
- **Ordering MUST be deterministic**: an explicit `ORDER BY` on a unique or tie-broken key.
  Non-deterministic ordering is a defect, not a style issue.
- For null filtering, use the explicit operator — `Filter("deactivated_at", Operator.Is, "null")`
  and its negated form — rather than comparing against the text `"null"`.

The canonical paged read:

```csharp
var query = client
    .From<PostRecord>()
    .Filter("author_id", Operator.Equals, userId.ToString())
    .Order("created_at", Ordering.Descending)
    .Order("id", Ordering.Descending);          // unique tie-breaker — stable page boundary

var totalCount = await SupabaseHelper.CallFunction<int>(
    client, "count_profile_posts", new { p_author_id = userId, p_search = searchTerm });

return await SocialMediaQueryUtility.GetPagedAsync(query, currentPage, itemsPerPage, totalCount);
```

Writes follow fetch → mutate → `Upsert` with
`new QueryOptions { Returning = QueryOptions.ReturnType.Representation }`, returning the model
from the response.

### 3.3 Exceptions

- Custom exceptions MUST be thrown **inside repositories** for failed POST / PUT / PATCH /
  DELETE. Controllers MUST NOT throw them.
- A **single global middleware** MUST catch them and emit RFC 7807 `ProblemDetails`. It is the
  only place exceptions become HTTP responses; per-controller try/catch for HTTP mapping MUST
  NOT be used.
- That middleware holds the one authoritative exception → status mapping:
  `NotFoundException` → 404, `ValidationException` → 400, `ConflictException` → 409, everything
  else → 500.

---

## 4. Code Standards

- **DRY** — shared logic goes into utilities or base classes, never copy-paste.
- `System.Text.Json` throughout. `Newtonsoft.Json` MUST NOT be introduced.
- Every async DB call MUST be awaited. Fire-and-forget (`_ = SomethingAsync()`) is forbidden —
  an unobserved cache invalidation may simply not run.

### 4.1 Caching Contract

Binding, because stale cache is the likeliest source of user-visible bugs in a social app.

- GET endpoints returning user-scoped data MUST cache under a key containing the user id and the
  resource — `profile_posts:{user_id}:{page}`.
- Every entry MUST have an explicit TTL. Default **60 seconds** unless stated otherwise here.
- **Writes MUST invalidate reads, in the same operation:**
  - new post → evict `profile_posts:{author_id}:*` and affected feed keys
  - follow / unfollow → evict follower *and* following count keys for **both** users
  - like / repost / comment → evict the post's aggregate-count key
- The logged-in user MUST be written and read through `UserCacheService` — never re-fetched ad
  hoc via the repository.
- **Never cached, always read live:** direct messages, community-discussion message threads, and
  any real-time message-history endpoint.

---

## 5. Scope

Three apps share this backend. These boundaries are authoritative.

**SocialMedia** — view profiles; follow/unfollow; profile info, posts, and media; like, repost,
and comment on posts; join communities and community discussions; direct messages and
community-discussion messages; lists that can save posts, comments, users, communities,
discussions, and discussion messages. Nearly all of it requires a logged-in user, read from
cache per §4.1.

**Meetup** — join a group or event; direct-message local guides; events online or in person;
create groups and events for a user.

**Zook** — products across multiple categories; create, update, and delete products for a user.

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
