# Event Video Prerequisites (Plan A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the three blocking defects of the video spec (DEP-1/2/3) and stand up the isolated test harness, so Plan B (event video) can be built and tested on a trustworthy base.

**Architecture:** Re-enable the existing global `ExceptionHandlingMiddleware`. Add an `IAccessTokenVerifier` that validates the HS256 signature of the backend's *own* access tokens (issued by `TokenService`) and returns the user id. Create `tests/AlSaqr.Tests` (xUnit) with a Testcontainers fixture running throwaway Postgres + PostgREST, which the real `Supabase.Client` talks to.

**Tech Stack:** .NET 8, xUnit 2.9.2, Microsoft.AspNetCore.TestHost 8.0.20, Testcontainers.PostgreSql 4.1.0, Npgsql 8.0.5, PostgREST v12.2.3, System.IdentityModel.Tokens.Jwt 7.5.1 (already resolved transitively).

**Spec:** `specs/video-streaming-online-events.md` §0 (Blocking Prerequisites), §1.2, §Testing. Product context: `specs/video-streaming-online-events.prd.md` §3.6.

## Global Constraints

- Target framework `net8.0` for every project, including the test project.
- `System.Text.Json` only in new code; never add `Newtonsoft.Json` usages.
- Constructor injection only; register services against interfaces.
- Tests MUST NOT touch the shared dev Supabase project. Every DB test uses the Testcontainers fixture; no manual setup, no leftover data.
- The audio-spaces files are frozen: do not edit `SpacesController.cs`, `SpaceRepository`, `CloudflareCallsService`, `SpaceEventBroadcaster`, `SpaceReaperService`.
- Build command is `dotnet build AlSaqr.API.sln -c Release`. **Do not add `-warnaserror`**: the baseline has 304 warnings (verified 2026-10-04), so that flag fails before any change.
- **Docker must be installed and running** for Task 3 onward (Testcontainers). As of 2026-10-04 Docker is *not* installed on the dev machine — install Docker Desktop before starting Task 3.

## Corrections to the spec (verified against code on 2026-10-04)

These override the spec where they conflict; Plan B relies on them.

1. **Tokens are not Supabase tokens.** `AuthController` issues access tokens with `TokenService` (`AlSaqr.Infrastructure/TokenService.cs`): HS256, key = `Encoding.ASCII.GetBytes(JwtSettings:Secret)`, user id in `ClaimTypes.NameIdentifier`, which serializes as the JWT claim **`nameid`** (not `sub`), 14-day expiry. Spec §1.2's `SupabaseAuthConfig.JwtSecret` + `.Subject` would reject every real token. The verifier below uses `JwtSettings:Secret` and `nameid`.
2. **`[Table(..., Schema = ...)]` cannot compile.** `Supabase.Postgrest.Attributes.TableAttribute` (Postgrest 4.0.3) has only a `tableName` constructor. The schema is set once via `SupabaseOptions.Schema`; entities omit it (all 53 existing entities do).
3. **Per-test transaction rollback is impossible over PostgREST** — every HTTP request commits its own transaction. Isolation is instead a throwaway container per run plus `TRUNCATE` of every table before each test.
4. **`SupabaseOptions.RestUrlFormat` defaults to `"{0}/rest/v1"`**; bare PostgREST serves at `/`. The fixture sets `RestUrlFormat = "{0}"`.

## Review Focus

1. **A token signed with a different secret, or with `alg: none`** → verifier returns `Guid.Empty`. Pinned in Task 2 (`OtherSecret_ReturnsEmpty`, `UnsignedToken_ReturnsEmpty`).
2. **A real token from `TokenService` must verify** — the whole point; a wrong claim name or key encoding silently locks everyone out. Pinned in Task 2 (`TokenServiceToken_ReturnsUserId`).
3. **A token whose user id is not a GUID** (e.g. issued for a malformed id) → verifier returns `Guid.Empty` rather than throwing. Pinned in Task 2 (`NonGuidUserId_ReturnsEmpty`).
4. **500 responses must not leak exception messages** → detail is the generic text. Pinned in Task 1 (`UnexpectedException_HidesMessage`).
5. **Data from one test leaking into the next** → `ResetAsync` empties every table. Pinned in Task 3 (`Reset_RemovesSeededRows`).

---

## File Structure

| Path | Responsibility |
|---|---|
| `tests/AlSaqr.Tests/AlSaqr.Tests.csproj` | Test project (create) |
| `tests/AlSaqr.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs` | Pins the exception → status mapping |
| `AlSaqr.API/Program.cs` | Re-enable middleware (line 189); register verifier |
| `AlSaqr.Infrastructure/AccessTokenVerifier.cs` | `IAccessTokenVerifier` + implementation (create) |
| `tests/AlSaqr.Tests/Auth/AccessTokenVerifierTests.cs` | Verifier tests |
| `tests/AlSaqr.Tests/Support/PostgrestFixture.cs` | Postgres + PostgREST containers, schema, reset |
| `tests/AlSaqr.Tests/Support/Seed.cs` | SQL seeding helpers |
| `tests/AlSaqr.Tests/Sql/00_core_tables.sql` | Test-only stand-ins for prod tables whose DDL is not in the repo |
| `tests/AlSaqr.Tests/Support/PostgrestHarnessTests.cs` | Proves the harness works |
| `.github/workflows/tests.yml` | CI (create) |

---

### Task 1: Test project + re-enable the global exception middleware (DEP-1)

**Files:**
- Create: `tests/AlSaqr.Tests/AlSaqr.Tests.csproj`
- Create: `tests/AlSaqr.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs`
- Modify: `AlSaqr.API.sln` (add project)
- Modify: `AlSaqr.API/Program.cs:189`

**Interfaces:**
- Consumes: `AlSaqr.API.Middleware.ExceptionHandlingMiddleware`, `AlSaqr.Domain.Common.{NotFound,Validation,Forbidden,Conflict}Exception`
- Produces: the test project every later task adds to.

- [ ] **Step 1: Create the test project**

`tests/AlSaqr.Tests/AlSaqr.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="8.0.20" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.1.0" />
    <PackageReference Include="Npgsql" Version="8.0.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\AlSaqr.API\AlSaqr.API.csproj" />
    <ProjectReference Include="..\..\AlSaqr.Data\AlSaqr.Data.csproj" />
    <ProjectReference Include="..\..\AlSaqr.Domain\AlSaqr.Domain.csproj" />
    <ProjectReference Include="..\..\AlSaqr.Infrastructure\AlSaqr.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Sql\**\*.sql" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln AlSaqr.API.sln add tests/AlSaqr.Tests/AlSaqr.Tests.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 2: Write the middleware tests**

These pin behavior that already exists in `ExceptionHandlingMiddleware`, so they are expected to pass on first run. Step 3 proves they can fail.

`tests/AlSaqr.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AlSaqr.API.Middleware;
using AlSaqr.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlSaqr.Tests.Middleware
{
    public class ExceptionHandlingMiddlewareTests
    {
        private static async Task<HttpResponseMessage> SendThrowing(Exception toThrow)
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services => services.AddLogging())
                    .Configure(app =>
                    {
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        app.Run(_ => throw toThrow);
                    }))
                .StartAsync();

            return await host.GetTestClient().GetAsync("/api/anything");
        }

        public static TheoryData<Exception, HttpStatusCode> MappedExceptions => new()
        {
            { new NotFoundException("missing"), HttpStatusCode.NotFound },
            { new ValidationException("bad input"), HttpStatusCode.BadRequest },
            { new ForbiddenException("not yours"), HttpStatusCode.Forbidden },
            { new ConflictException("already live"), HttpStatusCode.Conflict },
        };

        [Theory]
        [MemberData(nameof(MappedExceptions))]
        public async Task MappedException_ReturnsProblemDetailsWithStatusAndMessage(
            Exception exception, HttpStatusCode expected)
        {
            var response = await SendThrowing(exception);

            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal((int)expected, problem!.Status);
            Assert.Equal(exception.Message, problem.Detail);
            Assert.Equal("/api/anything", problem.Instance);
        }

        [Fact]
        public async Task UnexpectedException_Returns500_AndHidesMessage()
        {
            var response = await SendThrowing(new InvalidOperationException("conn=secret-host"));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal("An unexpected error occurred.", problem!.Detail);
            Assert.DoesNotContain("secret-host", await response.Content.ReadAsStringAsync());
        }
    }
}
```

- [ ] **Step 3: Run, then prove the tests can fail**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~ExceptionHandlingMiddlewareTests"`
Expected: PASS (5 tests).

Then temporarily delete the line `ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),` in `AlSaqr.API/Middleware/ExceptionHandlingMiddleware.cs` and re-run.
Expected: FAIL — the `ForbiddenException` case reports `Expected: Forbidden, Actual: InternalServerError`.
Restore the line (`git checkout AlSaqr.API/Middleware/ExceptionHandlingMiddleware.cs`) and re-run → PASS.

- [ ] **Step 4: Re-enable the middleware**

In `AlSaqr.API/Program.cs`, line 189, change:

```csharp
// app.UseMiddleware<ExceptionHandlingMiddleware>();
```

to:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

Leave the two comment lines above it as they are. (It was commented out in commit `29823fc`, "Failed attempt to create twitter spaces"; existing controllers wrap their bodies in try/catch → `StatusCode(500, ...)`, so their responses don't change. Only exceptions that escape a controller now become ProblemDetails instead of the default 500.)

- [ ] **Step 5: Build and run the full suite**

Run: `dotnet build AlSaqr.API.sln -c Release; dotnet test AlSaqr.API.sln -c Release --no-build`
Expected: build `0 Error(s)`; tests PASS (5).

If live secrets are available, also run `/smoke`: step 4 there checks 404 bodies carry `type`/`title`/`status`.

- [ ] **Step 6: Commit**

```bash
git add tests/AlSaqr.Tests AlSaqr.API.sln AlSaqr.API/Program.cs
git commit -m "fix: re-enable global ProblemDetails middleware; add test project"
```

---

### Task 2: Signature-verified caller identity (DEP-2 + DEP-3)

**Files:**
- Create: `AlSaqr.Infrastructure/AccessTokenVerifier.cs`
- Create: `tests/AlSaqr.Tests/Auth/AccessTokenVerifierTests.cs`
- Modify: `AlSaqr.API/Program.cs` (register next to `TokenService`, ~line 76)

**Interfaces:**
- Consumes: `AlSaqr.Infrastructure.TokenService(string secret).GenerateTokens(string userId) → TokenResponse { AccessToken }`
- Produces: `AlSaqr.Infrastructure.IAccessTokenVerifier { Guid GetVerifiedUserId(string? authorizationHeader); }`. It returns `Guid.Empty` for anything untrusted. Plan B controllers inject it.

- [ ] **Step 1: Write the failing tests**

`tests/AlSaqr.Tests/Auth/AccessTokenVerifierTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlSaqr.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Tests.Auth
{
    public class AccessTokenVerifierTests
    {
        private const string Secret = "test-access-token-secret-0123456789-abcdefghij";
        private readonly AccessTokenVerifier _verifier = new(Secret);

        private static string Issue(string userId) =>
            new TokenService(Secret).GenerateTokens(userId).AccessToken;

        [Fact]
        public void TokenServiceToken_ReturnsUserId()
        {
            var userId = Guid.NewGuid();

            Assert.Equal(userId, _verifier.GetVerifiedUserId($"Bearer {Issue(userId.ToString())}"));
        }

        [Fact]
        public void RawTokenWithoutBearerPrefix_ReturnsUserId()
        {
            var userId = Guid.NewGuid();

            Assert.Equal(userId, _verifier.GetVerifiedUserId(Issue(userId.ToString())));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Bearer ")]
        [InlineData("Bearer not-a-jwt")]
        public void MissingOrMalformed_ReturnsEmpty(string? header)
        {
            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId(header));
        }

        [Fact]
        public void OtherSecret_ReturnsEmpty()
        {
            var forged = new TokenService("a-completely-different-secret-0123456789-xyz")
                .GenerateTokens(Guid.NewGuid().ToString()).AccessToken;

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {forged}"));
        }

        [Fact]
        public void TamperedPayload_ReturnsEmpty()
        {
            var parts = Issue(Guid.NewGuid().ToString()).Split('.');
            var forgedPayload = Base64Url(
                $"{{\"nameid\":\"{Guid.NewGuid()}\",\"exp\":{DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()}}}");

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {parts[0]}.{forgedPayload}.{parts[2]}"));
        }

        [Fact]
        public void UnsignedToken_ReturnsEmpty()
        {
            var header = Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = Base64Url(
                $"{{\"nameid\":\"{Guid.NewGuid()}\",\"exp\":{DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()}}}");

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {header}.{payload}."));
        }

        [Fact]
        public void ExpiredToken_ReturnsEmpty()
        {
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
            var expired = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                claims: new[] { new Claim("nameid", Guid.NewGuid().ToString()) },
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires: DateTime.UtcNow.AddHours(-1),
                signingCredentials: credentials));

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {expired}"));
        }

        [Fact]
        public void NonGuidUserId_ReturnsEmpty()
        {
            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {Issue("not-a-guid")}"));
        }

        private static string Base64Url(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~AccessTokenVerifierTests"`
Expected: build FAIL — `The type or namespace name 'AccessTokenVerifier' could not be found`.

- [ ] **Step 3: Implement**

`AlSaqr.Infrastructure/AccessTokenVerifier.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Infrastructure
{
    /// <summary>
    /// Resolves the acting user from an access token issued by <see cref="TokenService"/>,
    /// verifying its HS256 signature and lifetime first (specs/video-streaming-online-events.md
    /// §0 DEP-2/DEP-3). Auth.AccessTokenValidator only checks presence and expiry, and
    /// UserCacheService keeps one global "loggedInUser" slot, so neither can identify a
    /// caller. Use this wherever an action needs to know who is asking.
    /// </summary>
    public interface IAccessTokenVerifier
    {
        /// <summary>
        /// The verified user id, or Guid.Empty when the header is missing, the token is
        /// malformed, unsigned, signed with another key, expired, or carries no user id.
        /// </summary>
        Guid GetVerifiedUserId(string? authorizationHeader);
    }

    public sealed class AccessTokenVerifier : IAccessTokenVerifier
    {
        // TokenService puts the user id in ClaimTypes.NameIdentifier, which the JWT
        // handler writes under the short claim name "nameid".
        private const string UserIdClaim = "nameid";

        private readonly TokenValidationParameters _parameters;

        public AccessTokenVerifier(string secret)
        {
            _parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                // Same key encoding TokenService signs with.
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ValidateLifetime = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }

        public Guid GetVerifiedUserId(string? authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader))
                return Guid.Empty;

            var token = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorizationHeader["Bearer ".Length..].Trim()
                : authorizationHeader.Trim();

            if (token.Length == 0)
                return Guid.Empty;

            try
            {
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                var principal = handler.ValidateToken(token, _parameters, out _);
                return Guid.TryParse(principal.FindFirst(UserIdClaim)?.Value, out var userId)
                    ? userId
                    : Guid.Empty;
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                // Forged, tampered, expired, or malformed: there is no trusted caller.
                return Guid.Empty;
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~AccessTokenVerifierTests"`
Expected: PASS (11 tests). If `TokenServiceToken_ReturnsUserId` fails while the negative tests pass, the claim name or key encoding is wrong. Fix the verifier, not the test.

- [ ] **Step 5: Register it**

In `AlSaqr.API/Program.cs`, directly after the `TokenService` registration block (`builder.Services.AddSingleton<TokenService>(...)`), add:

```csharp
// Signature-verified caller identity for actions that must know who is asking
// (specs/video-streaming-online-events.md §0 DEP-2/DEP-3). Same secret TokenService signs with.
builder.Services.AddSingleton<IAccessTokenVerifier>(_ => new AccessTokenVerifier(jwtSecret!));
```

Run: `dotnet build AlSaqr.API.sln -c Release; dotnet test AlSaqr.API.sln -c Release --no-build`
Expected: `0 Error(s)`; all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add AlSaqr.Infrastructure/AccessTokenVerifier.cs tests/AlSaqr.Tests/Auth AlSaqr.API/Program.cs
git commit -m "feat: add signature-verified access token verifier"
```

---

### Task 3: Isolated Postgres + PostgREST test harness

**Prerequisite:** Docker running (`docker info` succeeds).

**Files:**
- Create: `tests/AlSaqr.Tests/Sql/00_core_tables.sql`
- Create: `tests/AlSaqr.Tests/Support/PostgrestFixture.cs`
- Create: `tests/AlSaqr.Tests/Support/Seed.cs`
- Create: `tests/AlSaqr.Tests/Support/PostgrestHarnessTests.cs`

**Interfaces:**
- Produces (Plan B depends on all of these):
  - `[Collection(PostgrestCollection.Name)]` with `PostgrestCollection.Name == "postgrest"`
  - `PostgrestFixture.Client : Supabase.Client`, `PostgrestFixture.ConnectionString : string`, `PostgrestFixture.Seed : Seed`, `Task PostgrestFixture.ResetAsync()`
  - `PostgrestFixture.RepoSqlFiles : string[]` — repo-relative production DDL applied after the core tables (Plan B appends to it)
  - `Seed.User(string username, string? avatar = null) → Task<Guid>`, `Seed.Group(Guid founderId) → Task<Guid>`, `Seed.Event(Guid? groupId, bool isOnline) → Task<Guid>`, `Seed.Attendee(Guid userId) → Task<Guid>`, `Seed.EventAttendee(Guid eventId, Guid attendeeId, Guid groupId, bool isOrganizer = false) → Task<Guid>`, `Seed.Execute(string sql, params (string Name, object Value)[] parameters) → Task`, `Seed.Scalar<T>(string sql, params (string Name, object Value)[] parameters) → Task<T>`

- [ ] **Step 1: Write the core-table DDL**

The prod DDL for `users`, `groups`, `events`, `attendees` is not in the repo. These are **test-only stand-ins** with the columns the Meetup entities map. `event_attendees` uses the real file from `AlSaqr.Data`.

`tests/AlSaqr.Tests/Sql/00_core_tables.sql`:

```sql
-- Test-only stand-ins for production tables whose DDL is not in this repo.
-- Columns mirror the entity mappings in AlSaqr.Data/Entities. Real feature DDL
-- (event_attendees.sql, video_streams.sql, ...) is applied after this file.

create table "alsaqr-2026".users (
  id uuid not null default gen_random_uuid () primary key,
  username character varying not null,
  avatar character varying null,
  created_at timestamp with time zone not null default now()
);

create table "alsaqr-2026".groups (
  id uuid not null default gen_random_uuid () primary key,
  slug character varying null,
  name character varying null,
  description text null,
  images text[] null,
  created_at timestamp with time zone not null default now(),
  hq_city_id uuid null,
  founder_id uuid null references "alsaqr-2026".users (id) on delete cascade
);

create table "alsaqr-2026".events (
  id uuid not null default gen_random_uuid () primary key,
  slug character varying null,
  name character varying null,
  description text null,
  images text[] null,
  group_id uuid null references "alsaqr-2026".groups (id) on delete cascade,
  is_online boolean not null default false,
  last_occurred_at timestamp with time zone null,
  times_occurred integer null,
  created_at timestamp with time zone not null default now(),
  updated_at timestamp with time zone null
);

create table "alsaqr-2026".attendees (
  id uuid not null default gen_random_uuid () primary key,
  name character varying null,
  user_id uuid null references "alsaqr-2026".users (id) on delete cascade,
  created_at timestamp with time zone not null default now()
);
```

- [ ] **Step 2: Write the failing harness tests**

`tests/AlSaqr.Tests/Support/PostgrestHarnessTests.cs`:

```csharp
using AlSaqr.Data.Entities.Meetup;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Tests.Support
{
    [Collection(PostgrestCollection.Name)]
    public class PostgrestHarnessTests : IAsyncLifetime
    {
        private readonly PostgrestFixture _fx;

        public PostgrestHarnessTests(PostgrestFixture fx) => _fx = fx;

        public Task InitializeAsync() => _fx.ResetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SupabaseClient_ReadsSeededEvent_ThroughPostgrest()
        {
            var founder = await _fx.Seed.User("founder");
            var group = await _fx.Seed.Group(founder);
            var eventId = await _fx.Seed.Event(group, isOnline: true);

            var loaded = await _fx.Client
                .From<Event>()
                .Filter("id", Operator.Equals, eventId.ToString())
                .Single();

            Assert.NotNull(loaded);
            Assert.True(loaded!.IsOnline);
            Assert.Equal(group, loaded.GroupId);
        }

        [Fact]
        public async Task EventAttendeesTable_FromRepoDdl_IsQueryable()
        {
            var user = await _fx.Seed.User("attendee");
            var founder = await _fx.Seed.User("founder");
            var group = await _fx.Seed.Group(founder);
            var eventId = await _fx.Seed.Event(group, isOnline: true);
            var attendee = await _fx.Seed.Attendee(user);
            await _fx.Seed.EventAttendee(eventId, attendee, group, isOrganizer: true);

            var row = await _fx.Client
                .From<EventAttendees>()
                .Filter("event_id", Operator.Equals, eventId.ToString())
                .Single();

            Assert.True(row!.IsEventOrganizer);
        }

        [Fact]
        public async Task Reset_RemovesSeededRows()
        {
            await _fx.Seed.User("leftover");

            await _fx.ResetAsync();

            Assert.Equal(0L, await _fx.Seed.Scalar<long>("select count(*) from \"alsaqr-2026\".users"));
        }
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~PostgrestHarnessTests"`
Expected: build FAIL — `The type or namespace name 'PostgrestFixture' could not be found`.

- [ ] **Step 4: Implement the fixture and seeder**

`tests/AlSaqr.Tests/Support/PostgrestFixture.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AlSaqr.Tests.Support
{
    [CollectionDefinition(Name)]
    public sealed class PostgrestCollection : ICollectionFixture<PostgrestFixture>
    {
        public const string Name = "postgrest";
    }

    /// <summary>
    /// Throwaway Postgres + PostgREST per test run (CLAUDE.md "Tests"): no shared dev
    /// DB, no manual setup. PostgREST is required because the repositories speak
    /// PostgREST through Supabase.Client, not raw SQL. Isolation between tests is
    /// ResetAsync (TRUNCATE everything): PostgREST commits each request, so a
    /// per-test transaction rollback is not possible.
    /// </summary>
    public sealed class PostgrestFixture : IAsyncLifetime
    {
        public const string Schema = "alsaqr-2026";

        // PostgREST requires a JWT secret of at least 32 characters.
        private const string PostgrestJwtSecret = "alsaqr-test-postgrest-secret-0123456789abcdef";

        /// <summary>Production DDL, applied in order after the test-only core tables.</summary>
        public static readonly string[] RepoSqlFiles =
        {
            "AlSaqr.Data/Entities/Meetup/sql/event_attendees.sql",
        };

        private readonly INetwork _network;
        private readonly PostgreSqlContainer _db;
        private readonly IContainer _postgrest;

        public Supabase.Client Client { get; private set; } = default!;

        public string ConnectionString => _db.GetConnectionString();

        public Seed Seed { get; private set; } = default!;

        public PostgrestFixture()
        {
            _network = new NetworkBuilder().Build();

            _db = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithNetwork(_network)
                .WithNetworkAliases("db")
                .Build();

            _postgrest = new ContainerBuilder()
                .WithImage("postgrest/postgrest:v12.2.3")
                .WithNetwork(_network)
                .WithEnvironment("PGRST_DB_URI", "postgres://postgres:postgres@db:5432/postgres")
                .WithEnvironment("PGRST_DB_SCHEMAS", Schema)
                .WithEnvironment("PGRST_DB_ANON_ROLE", "postgres")
                .WithEnvironment("PGRST_JWT_SECRET", PostgrestJwtSecret)
                .WithPortBinding(3000, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(3000).ForPath("/")))
                .Build();
        }

        public async Task InitializeAsync()
        {
            await _network.CreateAsync();
            await _db.StartAsync();
            await ApplySchemaAsync();

            // PostgREST reads the schema cache at startup, so it starts after the DDL.
            await _postgrest.StartAsync();

            Client = new Supabase.Client(
                $"http://{_postgrest.Hostname}:{_postgrest.GetMappedPublicPort(3000)}",
                ServiceJwt(),
                new Supabase.SupabaseOptions
                {
                    Schema = Schema,
                    AutoConnectRealtime = false,
                    // Bare PostgREST serves at "/", not Supabase's "/rest/v1".
                    RestUrlFormat = "{0}",
                });

            Seed = new Seed(ConnectionString);
        }

        public async Task DisposeAsync()
        {
            await _postgrest.DisposeAsync();
            await _db.DisposeAsync();
            await _network.DeleteAsync();
        }

        /// <summary>Empties every table in the schema. Call at the start of each test.</summary>
        public async Task ResetAsync()
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            var tables = new List<string>();
            await using (var list = new NpgsqlCommand(
                "select tablename from pg_tables where schemaname = @schema", conn))
            {
                list.Parameters.AddWithValue("schema", Schema);
                await using var reader = await list.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    tables.Add($"\"{Schema}\".\"{reader.GetString(0)}\"");
            }

            if (tables.Count == 0)
                return;

            await using var truncate = new NpgsqlCommand(
                $"truncate table {string.Join(", ", tables)} restart identity cascade", conn);
            await truncate.ExecuteNonQueryAsync();
        }

        private async Task ApplySchemaAsync()
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            var scripts = new List<string> { $"create schema if not exists \"{Schema}\";" };

            var testSqlDir = Path.Combine(AppContext.BaseDirectory, "Sql");
            scripts.AddRange(Directory.GetFiles(testSqlDir, "*.sql").OrderBy(f => f, StringComparer.Ordinal)
                .Select(File.ReadAllText));

            var repoRoot = FindRepoRoot();
            scripts.AddRange(RepoSqlFiles.Select(f => File.ReadAllText(Path.Combine(repoRoot, f))));

            foreach (var sql in scripts)
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlSaqr.API.sln")))
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new InvalidOperationException("Could not locate AlSaqr.API.sln above the test output.");
        }

        private static string ServiceJwt()
        {
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PostgrestJwtSecret)),
                SecurityAlgorithms.HmacSha256);

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                claims: new[] { new Claim("role", "postgres") },
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: credentials));
        }
    }
}
```

`tests/AlSaqr.Tests/Support/Seed.cs`:

```csharp
using Npgsql;

namespace AlSaqr.Tests.Support
{
    /// <summary>
    /// Inserts fixture rows with plain SQL (not through entity models), so tests
    /// control every column and never depend on the code under test to set up state.
    /// </summary>
    public sealed class Seed
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly string _connectionString;

        public Seed(string connectionString) => _connectionString = connectionString;

        public Task<Guid> User(string username, string? avatar = null) =>
            Scalar<Guid>(
                $"insert into {S}.users (username, avatar) values (@username, @avatar) returning id",
                ("username", username), ("avatar", (object?)avatar ?? DBNull.Value));

        public Task<Guid> Group(Guid founderId) =>
            Scalar<Guid>(
                $"insert into {S}.groups (name, slug, founder_id) values ('Test Group', @slug, @founder) returning id",
                ("slug", $"group-{Guid.NewGuid():N}"), ("founder", founderId));

        public Task<Guid> Event(Guid? groupId, bool isOnline) =>
            Scalar<Guid>(
                $"insert into {S}.events (name, slug, group_id, is_online) values ('Test Event', @slug, @group, @online) returning id",
                ("slug", $"event-{Guid.NewGuid():N}"),
                ("group", (object?)groupId ?? DBNull.Value),
                ("online", isOnline));

        public Task<Guid> Attendee(Guid userId) =>
            Scalar<Guid>(
                $"insert into {S}.attendees (name, user_id) values ('Test Attendee', @user) returning id",
                ("user", userId));

        public Task<Guid> EventAttendee(Guid eventId, Guid attendeeId, Guid groupId, bool isOrganizer = false) =>
            Scalar<Guid>(
                $"insert into {S}.event_attendees (event_id, attendee_id, group_id, is_event_organizer) " +
                "values (@event, @attendee, @group, @organizer) returning id",
                ("event", eventId), ("attendee", attendeeId), ("group", groupId), ("organizer", isOrganizer));

        public async Task Execute(string sql, params (string Name, object Value)[] parameters)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = Command(sql, conn, parameters);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<T> Scalar<T>(string sql, params (string Name, object Value)[] parameters)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = Command(sql, conn, parameters);
            var result = await cmd.ExecuteScalarAsync();
            return result is null or DBNull ? default! : (T)result;
        }

        private static NpgsqlCommand Command(
            string sql, NpgsqlConnection conn, (string Name, object Value)[] parameters)
        {
            var cmd = new NpgsqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value);
            return cmd;
        }
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/AlSaqr.Tests --filter "FullyQualifiedName~PostgrestHarnessTests"`
Expected: PASS (3 tests). First run pulls `postgres:16-alpine` and `postgrest/postgrest:v12.2.3`, so allow a few minutes.

If `SupabaseClient_ReadsSeededEvent_ThroughPostgrest` fails with a 401/JWT error, PostgREST rejected the client's `Authorization` header: check `PGRST_JWT_SECRET` matches `PostgrestJwtSecret`. If it fails with 404 on `/rest/v1/events`, `RestUrlFormat` was not applied.

- [ ] **Step 6: Run the full suite and commit**

Run: `dotnet test AlSaqr.API.sln -c Release`
Expected: PASS (19 tests).

```bash
git add tests/AlSaqr.Tests/Sql tests/AlSaqr.Tests/Support
git commit -m "test: add isolated Postgres + PostgREST harness"
```

---

### Task 4: Run the tests in CI

**Files:**
- Create: `.github/workflows/tests.yml`

- [ ] **Step 1: Add the workflow**

```yaml
name: tests

on:
  push:
    branches: [main]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet restore AlSaqr.API.sln
      - run: dotnet build AlSaqr.API.sln --no-restore -c Release
      # GitHub-hosted Ubuntu runners ship Docker, which Testcontainers needs.
      - run: dotnet test AlSaqr.API.sln --no-build -c Release
```

- [ ] **Step 2: Verify locally and commit**

Run: `dotnet build AlSaqr.API.sln -c Release; dotnet test AlSaqr.API.sln -c Release --no-build`
Expected: PASS (19 tests).

```bash
git add .github/workflows/tests.yml
git commit -m "ci: run tests with Testcontainers on GitHub Actions"
```

After pushing, confirm the `tests` workflow is green on GitHub (`https://github.com/AliA1997/alsaqr-backend-api-v2/actions`). This is the first Linux build of the solution, so a path-casing failure is possible. Fix that in the offending file, not in the workflow.
