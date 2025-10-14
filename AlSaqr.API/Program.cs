using AlSaqr.API.Config;
using AlSaqr.API.Extensions;
using AlSaqr.Data;
using AlSaqr.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
// Retrieve Neo4j settings from configuration
var neo4jSettings = builder.Configuration.GetSection("GraphDB");
var uri = neo4jSettings["Uri"];
var username = neo4jSettings["Username"];
var password = neo4jSettings["Password"];
var database = neo4jSettings["Database"];

// Register IDriver as a singleton
builder.Services.AddSingleton<IDriver>(sp =>
    GraphDatabase.Driver(uri, AuthTokens.Basic(username, password))
);

// Add services to the container.

builder.Services.AddControllers(o => o.UseRoutePrefix("api"));

// Add Identity (optional but recommended)
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("AlSaqr"));
builder.Services.AddIdentityApiEndpoints<User>().AddEntityFrameworkStores<AppDbContext>();

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
})
.AddGoogle(opts =>
{
    opts.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    opts.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    opts.CallbackPath = "/api/Auth/signin-google";
})
.AddFacebook(opts =>
{
    opts.AppId = builder.Configuration["Authentication:Facebook:AppId"];
    opts.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
    opts.CallbackPath = "/api/Auth/signin-facebook";
})
.AddDiscord(opts =>
{
    opts.ClientId = builder.Configuration["Authentication:Discord:ClientId"];
    opts.ClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"];
    opts.CallbackPath = "/api/Auth/signin-discord";

    opts.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
    opts.TokenEndpoint = "https://discord.com/api/oauth2/token";
    opts.UserInformationEndpoint = "https://discord.com/api/users/@me";

    opts.Scope.Add("identify");
    opts.Scope.Add("email");

    opts.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    opts.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
    opts.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");



    opts.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            var response = await context.Backchannel.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            context.RunClaimActions(user);
        }
    };
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register configuration class
builder.Services.Configure<GraphDbConfig>(builder.Configuration.GetSection("GraphDB"));
builder.Services.Configure<MongoDbConfig>(builder.Configuration.GetSection("MongoDB"));

builder.Services.AddHealthChecks();
var app = builder.Build();

app.MapHealthChecks("/healthz");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

CancellationTokenSource cancellation = new();
app.Lifetime.ApplicationStopping.Register(() =>
{
    cancellation.Cancel();
});

app.Run();
