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
