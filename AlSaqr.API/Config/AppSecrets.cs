namespace AlSaqr.API.Config
{
    public sealed class AppSecrets
    {
        public string NewsApiKey { get; set; } = default!;
        public GraphDBSettings GraphDB { get; set; } = new();
        public MongoDBSettings MongoDB { get; set; } = new();
        public JwtSettings JwtSettings { get; set; } = new();
        public SupabaseSettings Supabase { get; set; } = new();
        public GoogleGeminiSettings GoogleGemini { get; set; } = new();
    }

    public sealed class GraphDBSettings
    {
        public string Uri { get; set; } = default!;
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string Database { get; set; } = default!;
    }

    public sealed class MongoDBSettings
    {
        public string MongoUri { get; set; } = default!;
        public string MongoUsername { get; set; } = default!;
        public string MongoPwd { get; set; } = default!;
        public string MongoDatabase { get; set; } = default!;
    }

    public sealed class JwtSettings
    {
        public string Key { get; set; } = default!;
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;
        public int ExpiryMinutes { get; set; }
    }

    public sealed class SupabaseSettings
    {
        public string Url { get; set; } = default!;
        public string Schema { get; set; } = default!;
        public string Key { get; set; } = default!;
    }

    public sealed class GoogleGeminiSettings
    {
        public string ApiKey { get; set; } = default!;
        public string Model { get; set; } = default!;
        public string BaseUrl { get; set; } = default!;
    }
}
