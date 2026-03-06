using CyclingAnalyzer.Api.Settings;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StravaSettings>(
    builder.Configuration.GetSection("Strava"));

builder.Services.AddHttpClient("strava");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Default")));

// Register services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<RideIngestionService>();

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