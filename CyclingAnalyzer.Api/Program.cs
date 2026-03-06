using CyclingAnalyzer.Api.Settings;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Bind Strava settings from user-secrets / appsettings
builder.Services.Configure<StravaSettings>(
    builder.Configuration.GetSection("Strava"));

// Named HttpClient for Strava calls
builder.Services.AddHttpClient("strava");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();