using AlSaqr.API.Config;
using AlSaqr.API.Extensions;
using AlSaqr.Data;
using AlSaqr.Data.Repositories.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Data.Repositories.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Data.Repositories.Zook;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using NewsAPI;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
// Retrieve Neo4j settings from configuration
var neo4jSettings = builder.Configuration.GetSection("GraphDB");
// Retrieve Supabase settings from configuration
var supabaseSettings = builder.Configuration.GetSection("Supabase");
var uri = neo4jSettings["Uri"];
var username = neo4jSettings["Username"];
var password = neo4jSettings["Password"];
var database = neo4jSettings["Database"];
IConfiguration configuration = builder.Configuration;

// Add services to the container.
// Register IDriver as a singleton
builder.Services.AddSingleton<IDriver>(sp =>
    GraphDatabase.Driver(uri, AuthTokens.Basic(username, password))
);

var supabaseUrl = supabaseSettings["Url"];
var supabaseKey = supabaseSettings["Key"];
var supabaseSchema = supabaseSettings["Schema"];
builder.Services.AddSingleton<Supabase.Client>(sp =>
{
    var options = new Supabase.SupabaseOptions { AutoConnectRealtime = true, Schema = supabaseSchema };
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
builder.Services.AddScoped<ICommunityDiscussionMemberRepository, CommunityDiscussionMemberRepository>();
builder.Services.AddScoped<ICommunityDiscussionMessageRepository, CommunityDiscussionMessageRepository>();
builder.Services.AddScoped<ICommunityMemberRepository, CommunityMemberRepository>();
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostStatusRepository, PostStatusRepository>();
builder.Services.AddScoped<IUserFollowRepository, UserFollowRepository>();
builder.Services.AddScoped<IListRepository, ListRepository>();
builder.Services.AddScoped<IListItemRepository, ListItemRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

builder.Services.AddHostedService<SupabaseInitializer>();


builder.Services.AddControllers(o => o.UseRoutePrefix("api"));

builder.Services.AddSingleton<NewsApiClient>(o =>
{
    var apiKey = configuration["NewsApiKey"];
    return new NewsApiClient(apiKey);
});
builder.Services.AddSingleton<IUserCacheService, UserCacheService>(_ => UserCacheService.Instance);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISocialMediaCacheService, SocialMediaCacheService>();


// Add Identity (optional but recommended)
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("AlSaqr"));
builder.Services.AddIdentityApiEndpoints<AlSaqr.Domain.Common.User>().AddEntityFrameworkStores<AppDbContext>();

// Add JWT authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

builder.Services.AddAuthentication(opts =>
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
builder.Services.Configure<GraphDbConfig>(builder.Configuration.GetSection("GraphDB"));
builder.Services.Configure<MongoDbConfig>(builder.Configuration.GetSection("MongoDB"));

builder.Services.AddHealthChecks();


// Read allowed origins from configuration
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
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
