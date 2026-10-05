# AlSaqr Backend

.NET 8 API serving three apps (SocialMedia, Meetup, Zook) from one Supabase PostgreSQL database.
What each app covers is in [specs/app-scope.md](specs/app-scope.md); feature specs live in [specs/](specs/).
These are rules for new and refactored code. A refactor brings code into compliance, never out of it.

## Commands

```bash
dotnet build AlSaqr.API.sln -c Release -warnaserror
dotnet format AlSaqr.API.sln --verify-no-changes
dotnet test AlSaqr.API.sln
```

Use `/validate` for the full suite and `/smoke` for a read-only live check.

## Projects

| Project | Owns | Never contains |
|---|---|---|
| `AlSaqr.API` | controllers, request/response mapping, the exception middleware | data access, caching, business rules |
| `AlSaqr.Domain` | DTOs only (Requests / Responses / Shared) | entities |
| `AlSaqr.Infrastructure` | cross-cutting services, mainly caching | |
| `AlSaqr.Data` | entities mapped to Supabase tables, repositories | |

- "Domain" means DTOs here, and entities live in `AlSaqr.Data`. This is deliberate, so don't move them.
- These four projects are the whole solution. Don't add a fifth (the old `AlSaqr.Services` was
  removed); adding a project is an architecture decision.

## Data access

- All queries go through a repository using the Supabase PostgREST client. EF Core is referenced
  but is not the data path, so don't write EF queries. Controllers never touch the Supabase client.
- Pass `Supabase.Client` into repository **methods** as a parameter. Never store it as repository state.
- Use constructor injection only, and register every repository and service against an interface
  (`IUserRepository`, `ISocialMediaCacheService`).
- Every entity carries `[Table("name", Schema = "alsaqr-2026")]`. Don't repeat the schema in
  filter paths: write `Filter("author_id", …)`, not `Filter("alsaqr-2026.author_id", …)`.
- Map `json` aggregate columns to `string?`. Map `jsonb` the same way unless it's deserialized into a typed model.
- Filter nulls with `Filter("col", Operator.Is, "null")` or its negated form, never by comparing to the text `"null"`.
- Writes go fetch → mutate → `Upsert` with
  `new QueryOptions { Returning = QueryOptions.ReturnType.Representation }`, and return the model from the response.

### Paginated reads

- Endpoints accept `currentPage`, `itemsPerPage`, and `searchTerm` as query params and pass them to the repository.
- Return `PaginatedResult<T>` (`AlSaqr.Domain/Utils/Common.cs`).
- Get the total from a PostgreSQL RPC via `SupabaseHelper.CallFunction`. Never count a fetched page client-side.
- Ordering must be deterministic: order by a unique key, or add a unique tie-breaker such as
  `.Order("id", …)`. Non-deterministic ordering is a defect.
- Reference implementation: `GroupRepository.GetGroupMembers` in
  [AlSaqr.Data/Repositories/Meetup/GroupRepository.cs](AlSaqr.Data/Repositories/Meetup/GroupRepository.cs).

## Errors

- Repositories throw the custom exceptions in `AlSaqr.Domain/Common/HttpMappedExceptions.cs` when
  a POST, PUT, PATCH, or DELETE fails. Controllers don't throw them.
- [ExceptionHandlingMiddleware](AlSaqr.API/Middleware/ExceptionHandlingMiddleware.cs) is the only place
  exceptions become HTTP responses (RFC 7807 `ProblemDetails`), and it holds the one exception → status
  mapping. Don't use try/catch in controllers to map errors to HTTP responses; add a case there instead.

## Code standards

- Use `System.Text.Json`. Existing `Newtonsoft.Json` usages are legacy, so don't add new ones.
- Await every async call. Fire-and-forget (`_ = SomethingAsync()`) is forbidden, because an
  unobserved cache invalidation may never run.
- Put shared logic in utilities or base classes rather than copying it.

## Caching

Stale cache is the likeliest user-visible bug in this app, so these rules are binding.

- GET endpoints returning user-scoped data cache under a key with the user id and resource,
  e.g. `profile_posts:{user_id}:{page}`, with an explicit TTL (default 60s).
- Every write invalidates the reads it affects, in the same operation:
  - new post → `profile_posts:{author_id}:*` and the affected feed keys
  - follow / unfollow → follower *and* following count keys for **both** users
  - like / repost / comment → the post's aggregate-count key
- Read and write the logged-in user through `UserCacheService`, never by re-fetching from a repository.
- Never cache direct messages, community-discussion message threads, or any real-time message history.

## New features

Work goes spec → plan → tasks → implement → validate. Each feature needs:
entities mapped from the table definition, an `I{Name}Repository` interface and implementation
(client as a method parameter), and GET methods cached per the rules above.

## Tests

- Never run tests against the shared dev database. Each run gets isolated, throwaway state:
  Testcontainers with PostgreSQL, or a test schema with per-test transaction rollback.
- Tests are deterministic and run in CI with no manual setup and no leftover data.
- Cover ordering, pagination boundaries, and cache invalidation, which are the historical defect areas.

## Done means

Every rule above is satisfied, deterministic tests cover the new behavior, no dead code is left,
and no cache entry exists without a matching invalidation.
