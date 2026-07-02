using System.Text;
using System.Text.Json;
using AlSaqr.API.Config;
using AlSaqr.API.Extensions;
using AlSaqr.Data;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Data.Repositories.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Data.Repositories.Yumna;
using AlSaqr.Data.Repositories.Yumna.Impl;
using AlSaqr.Data.Repositories.Zook;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Config;
using AlSaqr.Infrastructure.SocialMediaCache;
using AlSaqr.Infrastructure.Yumna;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using NewsAPI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
IConfiguration configuration = builder.Configuration;

var secretName = configuration["SecretsKey"];

// AWS values from environment variables
var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
var region = Environment.GetEnvironmentVariable("AWS_REGION");

var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
if (!string.IsNullOrWhiteSpace(secretName))
{
    using var secretsClient = new AmazonSecretsManagerClient(
        credentials,
        Amazon.RegionEndpoint.GetBySystemName(region)
    );

    var secretResponse = await secretsClient.GetSecretValueAsync(
        new GetSecretValueRequest { SecretId = secretName }
    );

    // Flatten: GraphDB__Uri -> GraphDB:Uri so the config binder nests it.
    var secretPairs = JsonSerializer
        .Deserialize<Dictionary<string, string>>(secretResponse.SecretString)!
        .ToDictionary(kvp => kvp.Key.Replace("__", ":"), kvp => (string?)kvp.Value);

    builder.Configuration.AddInMemoryCollection(secretPairs);
}
var supabaseUrl = configuration["Supabase:Url"];
var supabaseKey = configuration["Supabase:Key"];
var supabaseSchema = configuration["Supabase:Schema"];
var newsApiKey = configuration["NewsApiKey"];

builder.Services.AddSingleton<Supabase.Client>(sp =>
{
    var options = new Supabase.SupabaseOptions
    {
        AutoConnectRealtime = true,
        Schema = supabaseSchema,
    };
    return new Supabase.Client(supabaseUrl, supabaseKey, options);
});
builder.Services.AddScoped<ICityRepository, CityRepository>();
builder.Services.AddScoped<IAttendeeRepository, AttendeeRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<ILocalGuidesRepository, LocalGuidesRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ITopicRepository, TopicRepository>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ICommunityRepository, CommunityRepository>();
builder.Services.AddScoped<ICommunityDiscussionRepository, CommunityDiscussionRepository>();
builder.Services.AddScoped<
    ICommunityDiscussionMemberRepository,
    CommunityDiscussionMemberRepository
>();
builder.Services.AddScoped<
    ICommunityDiscussionMessageRepository,
    CommunityDiscussionMessageRepository
>();
builder.Services.AddScoped<ICommunityMemberRepository, CommunityMemberRepository>();
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostStatusRepository, PostStatusRepository>();
builder.Services.AddScoped<IUserFollowRepository, UserFollowRepository>();
builder.Services.AddScoped<IListRepository, ListRepository>();
builder.Services.AddScoped<IListItemRepository, ListItemRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.Configure<GoogleGeminiConfig>(builder.Configuration.GetSection("GoogleGemini"));
builder.Services.AddHttpClient<IYumnaService, YumnaService>();

builder.Services.AddHostedService<SupabaseInitializer>();

builder.Services.AddControllers(o => o.UseRoutePrefix("api"));

builder.Services.AddSingleton<NewsApiClient>(o =>
{
    var apiKey = newsApiKey;
    return new NewsApiClient(apiKey);
});
builder.Services.AddSingleton<IUserCacheService, UserCacheService>(_ => UserCacheService.Instance);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISocialMediaCacheService, SocialMediaCacheService>();

// Add Identity (optional but recommended)
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("AlSaqr"));
builder
    .Services.AddIdentityApiEndpoints<AlSaqr.Domain.Common.User>()
    .AddEntityFrameworkStores<AppDbContext>();

// Add JWT authentication

var key = Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"]!);

builder
    .Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(opts =>
    {
        opts.LoginPath = "/api/Auth/login";
        opts.LogoutPath = "/api/Auth/logout";
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register configuration class
builder.Services.AddHealthChecks();

// Read allowed origins from configuration
var allowedOrigins =
    configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowConfiguredOrigins",
        policy =>
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    );
});
var app = builder.Build();

app.MapHealthChecks("/healthz");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseCors("AllowConfiguredOrigins");
app.UseAuthorization();

app.MapControllers();

CancellationTokenSource cancellation = new();
app.Lifetime.ApplicationStopping.Register(() =>
{
    cancellation.Cancel();
});

app.Run();
