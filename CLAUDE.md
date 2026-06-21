# AlSaqr — Backend Project Constitution

This document **governs** how code is written, structured, and validated in the AlSaqr
backend. It is not a description of the current state; it is the set of rules an agent
(or human) MUST follow. Rules use RFC-2119 keywords: **MUST**, **MUST NOT**, **SHOULD**,
**MAY**. Anything not permitted by a MUST/SHOULD rule is out of scope and requires an
explicit decision before being added.

> **About the examples in this document.** Code blocks marked `✓` illustrate a compliant
> pattern; blocks marked `✗` show a violation to avoid. Examples are illustrative of the
> *pattern*, not drop-in code — method signatures, generic parameters, and helper names
> MUST match the actual utilities defined in the solution
> (`SocialMediaQueryUtility`, `SupabaseHelper`, the cache services, etc.).

---

## 1. Tech Stack

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

The DTO / entity split looks like this in practice:

```csharp
// AlSaqr.Domain — request DTO (input only)
public sealed record CreatePostRequest(string Text, Guid? CommunityId, string? MediaJson);

// AlSaqr.Domain — response DTO (output only; never reused as an input model)
public sealed record ProfilePostDto(
    Guid Id, Guid AuthorId, string Text, int LikeCount, int RepostCount, DateTime CreatedAt);

// AlSaqr.Data — entity mapped to the table (NOT a DTO; stays out of AlSaqr.Domain)
[Table("post", Schema = "alsaqr-2026")]
public sealed class PostRecord : BaseModel { /* ... see §3.2 ... */ }
```

---

## 3. Architectural Principles

### 3.1 Dependency Injection

- All dependencies **MUST** be supplied via **constructor injection** registered in the DI
  container. Property injection (`{ get; set; }` injected members) **MUST NOT** be used.
- Repositories and services **MUST** be registered against an interface
  (`IUserRepository`, `ISocialMediaCacheService`, etc.), never a concrete type.
- The `Supabase.Client` **MUST** be passed into repository methods as a parameter, per the
  established per-method convention — not stored as repository state.

```csharp
// ✓ Program.cs — register against interfaces (§3.1)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<ISocialMediaCacheService, SocialMediaCacheService>();
builder.Services.AddSingleton<IUserCacheService, UserCacheService>();
```

```csharp
// ✓ constructor injection only; depend on interfaces
public sealed class ProfileController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IUserCacheService _userCache;

    public ProfileController(IUserRepository users, IUserCacheService userCache)
    {
        _users = users;
        _userCache = userCache;
    }
}
```

```csharp
// ✗ MUST NOT — property injection
public sealed class ProfileController : ControllerBase
{
    public IUserRepository Users { get; set; } = default!; // forbidden
}
```

```csharp
// ✓ Supabase.Client passed per method (§3.1)
public Task<PaginatedResult<ProfilePostDto>> GetProfilePostsAsync(
    Supabase.Client client, Guid userId, int currentPage, int itemsPerPage, string? searchTerm);

// ✗ MUST NOT — client stored as repository state
public sealed class UserRepository : IUserRepository
{
    private readonly Supabase.Client _client;            // forbidden
    public UserRepository(Supabase.Client client) => _client = client;
}
```

### 3.2 Repository Pattern

- All PostgreSQL access **MUST** go through a repository that uses the Supabase PostgREST
  client. Controllers **MUST NOT** touch the Supabase client directly.
- Each entity **MUST** carry `[Table("name", Schema = "alsaqr-2026")]`. The schema MUST NOT
  be doubled in query paths.
- `json` aggregate columns **MUST** be mapped to `string?` in C#; `jsonb` columns follow the
  same rule unless deserialized into a typed model.
- When retrieving data, query params of currentPage, itemsPerPage, and searchTerm is passed into controller http endpoints.Then pass the query params in the repository for retrieving data.
- Paginated reads **MUST** return `PaginatedResult<T>` and use
  `SocialMediaQueryUtility.GetPagedAsync`. Count RPCs **MUST** go through
  `SupabaseHelper.CallFunction`.
- Ordering in any paged or list query **MUST** be deterministic (an explicit `ORDER BY` on a
  unique or tie-broken key). Non-deterministic ordering is a defect.

```csharp
// ✓ entity: Table attribute with schema; json column → string? (§3.2)
[Table("post", Schema = "alsaqr-2026")]
public sealed class PostRecord : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Column("text")]
    public string Text { get; set; } = default!;

    [Column("like_count")]
    public int LikeCount { get; set; }

    // json aggregate column MUST map to string? — not a typed model unless deserialized
    [Column("media")]
    public string? MediaJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
```

```csharp
// ✓ paginated read: PaginatedResult<T>, deterministic order, count via RPC helper (§3.2)
public async Task<PaginatedResult<PostRecord>> GetProfilePostsAsync(
    Supabase.Client client, Guid userId, int currentPage, int itemsPerPage, string? searchTerm)
{
    var query = client
        .From<PostRecord>()
        .Filter("author_id", Operator.Equals, userId.ToString());

    if (!string.IsNullOrWhiteSpace(searchTerm))
        query = query.Filter("text", Operator.ILike, $"%{searchTerm}%");

    // Deterministic ordering: primary sort key + unique tie-breaker. The trailing
    // .Order on "id" guarantees a stable page boundary when created_at ties.
    query = query
        .Order("created_at", Ordering.Descending)
        .Order("id", Ordering.Descending);

    // Count MUST go through SupabaseHelper.CallFunction (a PostgreSQL RPC), never a
    // client-side count of a fetched page.
    var totalCount = await SupabaseHelper.CallFunction<int>(
        client, "count_profile_posts", new { p_author_id = userId, p_search = searchTerm });

    // Pagination MUST go through the shared utility, which returns PaginatedResult<T>.
    return await SocialMediaQueryUtility.GetPagedAsync(
        query, currentPage, itemsPerPage, totalCount);
}
```

```csharp
// ✓ write path: fetch → mutate → Upsert with Representation; throw inside the repo (§3.2, §3.3)
public async Task<PostRecord> LikePostAsync(Supabase.Client client, Guid postId)
{
    var existing = await client
        .From<PostRecord>()
        .Filter("id", Operator.Equals, postId.ToString())
        .Single();

    if (existing is null)
        throw new NotFoundException($"Post {postId} not found.");

    existing.LikeCount += 1;

    var response = await client
        .From<PostRecord>()
        .Upsert(existing, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

    return response.Models.First();
}
```

```csharp
// ✓ null filtering convention: prefer the explicit NOT-NULL operator over a "null" string
var active = await client
    .From<UserRecord>()
    .Filter("deactivated_at", Operator.Is, "null")   // IS NULL
    .Get();

// For "column IS NOT NULL", use the negated form rather than comparing to the text "null".
```

```csharp
// ✗ MUST NOT — controller touches the Supabase client directly
[HttpGet("{userId:guid}/posts")]
public async Task<IActionResult> GetPosts(Guid userId, [FromServices] Supabase.Client client)
{
    var rows = await client.From<PostRecord>().Get(); // forbidden — goes through the repo
    return Ok(rows.Models);
}

// ✗ MUST NOT — doubling the schema in the query path
client.From<PostRecord>().Filter("alsaqr-2026.author_id", Operator.Equals, id); // wrong

// ✗ MUST NOT — non-deterministic ordering (no explicit, tie-broken ORDER BY)
client.From<PostRecord>().Range(0, 19).Get(); // order is undefined → defect
```

### 3.3 Exceptions

- Custom exceptions **MUST** be thrown inside repositories for failed POST / PUT / PATCH /
  DELETE operations. Controllers **MUST NOT** throw them.
- Exceptions **MUST** be caught and mapped to HTTP responses in a **single global exception
  middleware** that emits RFC 7807 `ProblemDetails`. Per-controller try/catch for the
  purpose of HTTP mapping **MUST NOT** be used.
- Each custom exception **MUST** map to a defined status code (e.g. not-found → 404,
  validation → 400, conflict → 409). This mapping lives in the middleware and is the single
  source of truth.

```csharp
// ✓ custom exceptions (thrown inside repositories only)
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
```

```csharp
// ✓ single global middleware — the ONLY place exceptions become HTTP responses (§3.3)
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    public ExceptionHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Single source of truth for exception → status code mapping.
            var (status, title) = ex switch
            {
                NotFoundException   => (StatusCodes.Status404NotFound,   "Resource not found"),
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                ConflictException   => (StatusCodes.Status409Conflict,   "Conflict"),
                _                   => (StatusCodes.Status500InternalServerError, "Unexpected error"),
            };

            var problem = new ProblemDetails
            {
                Status   = status,
                Title    = title,
                Detail   = ex.Message,
                Instance = context.Request.Path,
            };

            context.Response.StatusCode  = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}

// Registered once, early in the pipeline:
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

```csharp
// ✗ MUST NOT — HTTP mapping inside a controller (belongs in the middleware)
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id)
{
    try
    {
        await _posts.DeleteAsync(_client, id);
        return NoContent();
    }
    catch (NotFoundException)   // forbidden — let it bubble to the middleware
    {
        return NotFound();
    }
}
```

---

## 4. Code Standards

- **DRY** — shared logic MUST be factored into utilities or base classes, not copy-pasted.
- Code **MUST** use `System.Text.Json` throughout. `Newtonsoft.Json` MUST NOT be introduced.
- All async DB calls **MUST** be `await`ed; methods returning `Task` MUST NOT be fire-and-forget.

```csharp
// ✓ System.Text.Json only
using System.Text.Json;
var media = JsonSerializer.Deserialize<MediaDto[]>(post.MediaJson ?? "[]");

// ✗ MUST NOT — Newtonsoft
using Newtonsoft.Json;                                   // forbidden
var media = JsonConvert.DeserializeObject<MediaDto[]>(post.MediaJson); // forbidden
```

```csharp
// ✓ every async DB call is awaited
await _cache.RemoveByPrefixAsync($"profile_posts:{authorId}:");

// ✗ MUST NOT — fire-and-forget (unobserved Task; invalidation may not run)
_ = _cache.RemoveByPrefixAsync($"profile_posts:{authorId}:"); // forbidden
```

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

```csharp
// ✓ read: user-scoped key + explicit TTL (§4.1)
public async Task<PaginatedResult<ProfilePostDto>> GetProfilePostsAsync(
    Supabase.Client client, Guid userId, int page)
{
    var cacheKey = $"profile_posts:{userId}:{page}";       // includes user_id + resource

    var cached = await _cache.GetAsync<PaginatedResult<ProfilePostDto>>(cacheKey);
    if (cached is not null)
        return cached;

    var result = await _users.GetProfilePostsAsync(client, userId, page, 20, null);

    await _cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(60)); // explicit TTL
    return result;
}
```

```csharp
// ✓ write invalidates read, in the SAME operation (§4.1)
public async Task CreatePostAsync(Supabase.Client client, Guid authorId, CreatePostRequest req)
{
    await _posts.CreateAsync(client, authorId, req);

    await _cache.RemoveByPrefixAsync($"profile_posts:{authorId}:"); // evict author's posts
    await _cache.RemoveByPrefixAsync($"feed:{authorId}:");          // evict affected feeds
}
```

```csharp
// ✓ follow/unfollow evicts count keys for BOTH users (§4.1)
public async Task FollowAsync(Supabase.Client client, Guid followerId, Guid followeeId)
{
    await _users.FollowAsync(client, followerId, followeeId);

    await _cache.RemoveAsync($"following_count:{followerId}");
    await _cache.RemoveAsync($"follower_count:{followeeId}");
}
```

```csharp
// ✓ logged-in user ALWAYS via UserCacheService — never re-fetched ad hoc (§4.1)
var currentUser = await _userCache.GetCurrentUserAsync(sessionUserId);

// ✗ MUST NOT — ad-hoc re-fetch of the logged-in user bypasses UserCacheService
var currentUser = await _users.GetByIdAsync(client, sessionUserId); // forbidden
```

```csharp
// ✗ MUST NOT cache — messages are always read live (§4.1)
// direct messages, community-discussion threads, real-time message history
public Task<IReadOnlyList<MessageDto>> GetHistoryAsync(Supabase.Client client, Guid conversationId)
    => _messages.GetHistoryAsync(client, conversationId); // no cache layer
```

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

```csharp
// ✓ interface + implementation; client is a method parameter (§3.1, §6)
public interface IGroupRepository
{
    Task<PaginatedResult<GroupRecord>> GetGroupsAsync(
        Supabase.Client client, Guid userId, int currentPage, int itemsPerPage, string? searchTerm);

    Task<GroupRecord> CreateAsync(Supabase.Client client, Guid ownerId, CreateGroupRequest req);
}

public sealed class GroupRepository : IGroupRepository
{
    public async Task<PaginatedResult<GroupRecord>> GetGroupsAsync(
        Supabase.Client client, Guid userId, int currentPage, int itemsPerPage, string? searchTerm)
    {
        var query = client
            .From<GroupRecord>()
            .Filter("owner_id", Operator.Equals, userId.ToString())
            .Order("created_at", Ordering.Descending)
            .Order("id", Ordering.Descending);          // deterministic (§3.2)

        var total = await SupabaseHelper.CallFunction<int>(
            client, "count_user_groups", new { p_owner_id = userId, p_search = searchTerm });

        return await SocialMediaQueryUtility.GetPagedAsync(query, currentPage, itemsPerPage, total);
    }

    public async Task<GroupRecord> CreateAsync(
        Supabase.Client client, Guid ownerId, CreateGroupRequest req)
    {
        var entity = new GroupRecord { OwnerId = ownerId, Name = req.Name };

        var response = await client
            .From<GroupRecord>()
            .Insert(entity, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

        return response.Models.FirstOrDefault()
            ?? throw new ConflictException("Group could not be created."); // thrown in repo (§3.3)
    }
}
```

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

```csharp
// ✓ Testcontainers — throwaway PostgreSQL per run; no shared dev DB (§Validation)
public sealed class RepositoryFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _db.GetConnectionString();

    public Task InitializeAsync() => _db.StartAsync();   // disposable, isolated
    public Task DisposeAsync()    => _db.DisposeAsync().AsTask();
}
```

```csharp
// ✓ behavioral test asserts a defect-prone area: write invalidates read (§4.1, §Validation)
[Fact]
public async Task CreatePost_Evicts_ProfilePostsCache()
{
    await _service.GetProfilePostsAsync(_client, _authorId, page: 0); // warm the cache

    await _service.CreatePostAsync(_client, _authorId, new CreatePostRequest("hi", null, null));

    Assert.False(await _cache.ExistsAsync($"profile_posts:{_authorId}:0")); // evicted
}
```

---

## 7. Definition of "Done"

A change is done only when: it satisfies every applicable MUST rule, has deterministic tests
covering the new behavior, leaves no dead code in `AlSaqr.Services`, and does not introduce
caching without a matching invalidation rule.