using System.Text;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Services;
using CyclingAnalyzer.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Settings
builder.Services.Configure<StravaSettings>(
    builder.Configuration.GetSection("Strava"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Default")));

// HTTP clients
builder.Services.AddHttpClient("strava");

// Services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<RideIngestionService>();
builder.Services.AddScoped<JwtService>();

// JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
var key         = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings.Issuer,
            ValidAudience            = jwtSettings.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(key),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    // Allowed origins come from config so the same binary works in dev and prod.
    // Dev: appsettings.Development.json → ["http://localhost:5173"]
    // Prod: appsettings.json (or env var override) → your real domain
    var allowedOrigins = builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>() ?? [];

    options.AddPolicy("ReactApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Trust X-Forwarded-For / X-Forwarded-Proto headers set by nginx or an AWS
// load balancer. This must be configured before the middleware pipeline runs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear the default whitelist so any proxy is trusted.
    // Tighten this to specific IPs once you know your load balancer's address.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Must be first in the pipeline so every subsequent middleware sees the
// correct scheme/host as reported by the upstream proxy.
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthentication(); // must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.UseCors("ReactApp");

app.Run();