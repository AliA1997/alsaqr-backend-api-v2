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
            "AlSaqr.Data/Entities/Meetup/sql/video_streams.sql",
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
