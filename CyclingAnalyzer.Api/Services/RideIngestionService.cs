using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Models;
using CyclingAnalyzer.Api.Models.Entities;

namespace CyclingAnalyzer.Api.Services;

public class RideIngestionService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly HttpClient _http;
    private readonly ILogger<RideIngestionService> _logger;
    private static readonly HashSet<string> _cyclingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Ride", "VirtualRide", "GravelRide", "EBikeRide", "MountainBikeRide"
    };

    // Strava returns max 200 activities per page
    private const int PageSize = 200;

    public RideIngestionService(
        AppDbContext db,
        TokenService tokenService,
        IHttpClientFactory httpFactory,
        ILogger<RideIngestionService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _http = httpFactory.CreateClient("strava");
        _logger = logger;
    }

    public async Task<IngestResult> IngestRidesAsync(long athleteId)
    {
        var accessToken = await _tokenService.GetValidTokenAsync(athleteId);
        if (accessToken is null)
            return new IngestResult(false, 0, "No valid access token found.");

        var athlete = await _db.Athletes.FindAsync(athleteId);
        if (athlete is null)
            return new IngestResult(false, 0, "Athlete not found.");

        // Find the most recent ride we already have so we only fetch new ones
        var mostRecent = await _db.Rides
            .Where(r => r.AthleteId == athleteId)
            .OrderByDescending(r => r.StartDate)
            .FirstOrDefaultAsync();

        // Strava uses Unix epoch for the "after" filter
        var after = mostRecent is not null
            ? new DateTimeOffset(mostRecent.StartDate).ToUnixTimeSeconds()
            : 0; // 0 = fetch everything

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var newRides = new List<Ride>();
        var page = 1;
        var keepGoing = true;

        while (keepGoing)
        {
            var url = $"https://www.strava.com/api/v3/athlete/activities" +
                      $"?after={after}&per_page={PageSize}&page={page}";

            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Strava activities fetch failed on page {Page}: {Status}",
                    page, response.StatusCode);
                break;
            }

            var json = await response.Content.ReadAsStringAsync();
            var activities = JsonSerializer.Deserialize<List<StravaActivityResponse>>(json);

            if (activities is null || activities.Count == 0)
                break;

            foreach (var activity in activities)
            {
                // Only ingest cycling activities
                if (!IsCyclingActivity(activity)) continue;

                // Skip if we already have this ride
                var exists = await _db.Rides.AnyAsync(r => r.Id == activity.Id);
                if (exists) continue;

                newRides.Add(new Ride
                {
                    Id                   = activity.Id,
                    AthleteId            = athleteId,
                    Name                 = activity.Name,
                    DistanceMeters       = activity.Distance,
                    MovingTimeSeconds    = activity.MovingTime,
                    ElevationGainMeters  = activity.TotalElevationGain,
                    AveragePowerWatts    = activity.AverageWatts,
                    MaxPowerWatts        = activity.MaxWatts,
                    AverageHeartRate     = activity.AverageHeartrate,
                    MaxHeartRate         = activity.MaxHeartrate,
                    AverageSpeedMs       = activity.AverageSpeed,
                    WeightKgAtTime       = athlete.WeightKg,
                    StartDate            = activity.StartDate,
                });
            }

            // If we got fewer than a full page we have everything
            keepGoing = activities.Count == PageSize;
            page++;
        }

        if (newRides.Count > 0)
        {
            _db.Rides.AddRange(newRides);
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Ingested {Count} new rides for athlete {AthleteId}", newRides.Count, athleteId);

        return new IngestResult(true, newRides.Count, null);
    }

    private static bool IsCyclingActivity(StravaActivityResponse activity)
    {
        return _cyclingTypes.Contains(activity.SportType) || _cyclingTypes.Contains(activity.Type);
    }
}

// Simple result object to report back what happened
public record IngestResult(bool Success, int NewRidesCount, string? Error);