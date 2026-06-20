# AlSaqr — Backend Project Constitution

This document **governs** how code is written, structured, and validated in the AlSaqr
backend. It is not a description of the current state; it is the set of rules an agent
(or human) MUST follow. Rules use RFC-2119 keywords: **MUST**, **MUST NOT**, **SHOULD**,
**MAY**. Anything not permitted by a MUST/SHOULD rule is out of scope and requires an
explicit decision before being added.

---

## 1. Tech Stack

The project targets the following versions. An agent **MUST NOT** upgrade, downgrade, or
add packages without an explicit instruction.

| Package | Version |
| --- | --- |
| .NET | 8.0 |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.1 |
| Microsoft.EntityFrameworkCore | 8.0.20 |
| Microsoft.Extensions.Caching.* | (per solution lock file) |
| NewsAPI | 0.70 |
| Supabase | 1.0.5 |

> Note: EF Core is referenced but data access goes through the Supabase PostgREST client
> (see §3). If EF Core is **not** used for actual querying, this MUST be stated here, and
> the package SHOULD be removed to avoid implying an ORM access path that doesn't exist.

---

## 2. Project Structure

The solution is split into the following projects. Each has a single responsibility and
**MUST NOT** take on others'.

- **AlSaqr.API** — HTTP controllers only. MUST NOT contain data-access logic, caching
  logic, or business rules beyond request/response mapping.
- **AlSaqr.Domain** — DTO classes (Requests / Responses / Shared).
  > Naming note: "Domain" here means **DTOs**, not domain entities. Database entities live
  > in `AlSaqr.Data`. This deviates from the common convention where "Domain" = business
  > entities; the split is intentional and MUST be preserved as-is to match the existing
  > solution. Do not move entities into this project.
- **AlSaqr.Infrastructure** — cross-cutting services, primarily caching
  (`SocialMediaCacheService`, `UserCacheService`).
- **AlSaqr.Data** — entity classes mapped to Supabase PostgreSQL tables, plus repositories.
- **AlSaqr.Services** — **currently unused.** An agent MUST NOT add code here. This project
  exists as a placeholder for future "group services" functionality. If it remains unused,
  it SHOULD be removed from the solution rather than left as dead scaffolding.

---

## 3. Architectural Principles

### 3.1 Dependency Injection

- All dependencies **MUST** be supplied via **constructor injection** registered in the DI
  container. Property injection (`{ get; set; }` injected members) **MUST NOT** be used.
- Repositories and services **MUST** be registered against an interface
  (`IUserRepository`, `ISocialMediaCacheService`, etc.), never a concrete type.
- The `Supabase.Client` **MUST** be passed into repository methods as a parameter, per the
  established per-method convention — not stored as repository state.

### 3.2 Repository Pattern

- All PostgreSQL access **MUST** go through a repository that uses the Supabase PostgREST
  client. Controllers **MUST NOT** touch the Supabase client directly.
- Each entity **MUST** carry `[Table("name", Schema = "alsaqr-2026")]`. The schema MUST NOT
  be doubled in query paths.
- `json` aggregate columns **MUST** be mapped to `string?` in C#; `jsonb` columns follow the
  same rule unless deserialized into a typed model.
- Paginated reads **MUST** return `PaginatedResult<T>` and use
  `SocialMediaQueryUtility.GetPagedAsync`. Count RPCs **MUST** go through
  `SupabaseHelper.CallFunction`.
- Ordering in any paged or list query **MUST** be deterministic (an explicit `ORDER BY` on a
  unique or tie-broken key). Non-deterministic ordering is a defect.

### 3.3 Exceptions

- Custom exceptions **MUST** be thrown inside repositories for failed POST / PUT / PATCH /
  DELETE operations. Controllers **MUST NOT** throw them.
- Exceptions **MUST** be caught and mapped to HTTP responses in a **single global exception
  middleware** that emits RFC 7807 `ProblemDetails`. Per-controller try/catch for the
  purpose of HTTP mapping **MUST NOT** be used.
- Each custom exception **MUST** map to a defined status code (e.g. not-found → 404,
  validation → 400, conflict → 409). This mapping lives in the middleware and is the single
  source of truth.

---

## 4. Code Standards

- **DRY** — shared logic MUST be factored into utilities or base classes, not copy-pasted.
- Code **MUST** use `System.Text.Json` throughout. `Newtonsoft.Json` MUST NOT be introduced.
- All async DB calls **MUST** be `await`ed; methods returning `Task` MUST NOT be fire-and-forget.

### 4.1 Caching Contract

This section is binding because stale cache is the most likely source of user-visible bugs
in a social app.

- GET endpoints that return user-scoped data **MUST** cache by a key that includes the
  `user_id` and the resource (e.g. `profile_posts:{user_id}:{page}`).
- Every cache entry **MUST** have an explicit TTL. Default TTL is **60 seconds** unless the
  endpoint specifies otherwise here.
- **Writes MUST invalidate reads.** Any POST / PUT / PATCH / DELETE that changes a cached
  resource **MUST** evict the affected keys in the same operation. Examples:
  - A new post → evict `profile_posts:{author_id}:*` and any affected feed keys.
  - Follow / unfollow → evict follower/following count keys for **both** users.
  - Like / repost / comment → evict the post's aggregate-count key.
- The logged-in user **MUST** be written via `UserCacheService` and read via
  `UserCacheService` — never re-fetched ad hoc.
- The following **MUST NOT** be cached (always read live):
  - Direct messages and community-discussion message threads.
  - Any real-time message-history endpoint.

---

## 5. Specification (Scope)

Three apps share the backend. Scope boundaries below are authoritative.

### SocialMedia
- View other profiles; follow / unfollow users.
- View profile info, profile posts, and profile media.
- Posts: like, repost, comment.
- Comments: add to a post.
- Communities: join communities and community discussions.
- Messaging: direct messages between users; community-discussion messages.
- Lists: create lists; save posts, comments, users, communities, community discussions, and
  community-discussion messages to lists.
- **Auth:** nearly all functionality requires a logged-in user, retrieved from cache (§4.1).

### Meetup
- Join a group; join an event.
- Direct message local guides.
- Events MAY be online or in person.
- Create groups and events for a user.

### Zook
- Products shown across multiple categories.
- Create, update, and delete products for a user.

---

## 6. Workflow (SDD)

Implementation follows five phases: Specification → Technical Planning → Task Breakdown →
Implementation → Validation.

### Task Breakdown rules
- Create entity classes mapped to a PostgreSQL table from a provided table definition.
- Create an `I{Name}Repository` interface and implementation that take the Supabase client
  as a method parameter.
- GET methods cache by `user_id` per §4.1 unless this document exempts the endpoint.

### Implementation
- Status: functional but undergoing refactoring. Refactors **MUST NOT** weaken any MUST rule
  above; they bring code *into* compliance, never out of it.

### Validation
- Tests **MUST NOT** run against the shared dev database. Each test run **MUST** use an
  isolated, disposable state via one of:
  - **Testcontainers** spinning up a throwaway PostgreSQL instance, **or**
  - a dedicated test schema with **per-test transaction rollback**.
- Tests **MUST** be deterministic and runnable in CI without manual setup or leftover data.
- Behavioral tests **SHOULD** assert ordering, pagination boundaries, and cache invalidation,
  since these are the historically defect-prone areas.

---

## 7. Definition of "Done"

A change is done only when: it satisfies every applicable MUST rule, has deterministic tests
covering the new behavior, leaves no dead code in `AlSaqr.Services`, and does not introduce
caching without a matching invalidation rule.