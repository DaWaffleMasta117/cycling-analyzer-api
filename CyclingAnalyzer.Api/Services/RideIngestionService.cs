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

    private static readonly HashSet<string> _cyclingTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ride", "VirtualRide", "GravelRide", "EBikeRide", "MountainBikeRide"
        };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Strava returns max 200 activities per page
    private const int PageSize = 200;

    // Polite delay between Strava Streams API calls to stay well within
    // the 100-requests-per-15-min rate limit.
    private static readonly TimeSpan StreamFetchDelay = TimeSpan.FromMilliseconds(300);

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
            return new IngestResult(false, 0, 0, "No valid access token found.");

        var athlete = await _db.Athletes.FindAsync(athleteId);
        if (athlete is null)
            return new IngestResult(false, 0, 0, "Athlete not found.");

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // ----------------------------------------------------------------
        // Weight refresh — the OAuth token exchange returns a SummaryAthlete
        // without the weight field, so we fetch the full DetailedAthlete on
        // every sync to keep the stored weight current.
        // ----------------------------------------------------------------
        var profileResponse = await _http.GetAsync("https://www.strava.com/api/v3/athlete");
        if (profileResponse.IsSuccessStatusCode)
        {
            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            var detailedAthlete = JsonSerializer.Deserialize<StravaAthlete>(
                profileJson, _jsonOptions);

            if (detailedAthlete?.Weight > 0)
            {
                athlete.WeightKg  = detailedAthlete.Weight;
                athlete.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                _logger.LogInformation(
                    "Updated weight for athlete {AthleteId}: {Weight}kg",
                    athleteId, detailedAthlete.Weight);
            }
        }
        else
        {
            _logger.LogWarning(
                "Could not fetch athlete profile during sync: {Status}",
                profileResponse.StatusCode);
        }

        // ----------------------------------------------------------------
        // Phase 1 — ingest new rides from the activities list endpoint
        // ----------------------------------------------------------------
        var newRides = await FetchNewRidesAsync(athleteId, athlete);

        if (newRides.Count > 0)
        {
            _db.Rides.AddRange(newRides);
            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Ingested {Count} new rides for athlete {AthleteId}",
                newRides.Count, athleteId);
        }

        // ----------------------------------------------------------------
        // Phase 2 — fetch second-by-second power streams for every ride
        // that has a power meter (AveragePowerWatts > 0) but no stream yet.
        // This covers both newly added rides and any older rides that were
        // ingested before this feature was added.
        // ----------------------------------------------------------------
        var streamsAdded = await FetchMissingPowerStreamsAsync(athleteId);

        return new IngestResult(true, newRides.Count, streamsAdded, null);
    }

    // ------------------------------------------------------------------
    // Phase 1 helpers
    // ------------------------------------------------------------------

    private async Task<List<Ride>> FetchNewRidesAsync(long athleteId, Athlete athlete)
    {
        var mostRecent = await _db.Rides
            .Where(r => r.AthleteId == athleteId)
            .OrderByDescending(r => r.StartDate)
            .FirstOrDefaultAsync();

        var after = mostRecent is not null
            ? new DateTimeOffset(mostRecent.StartDate).ToUnixTimeSeconds()
            : 0L;

        var newRides = new List<Ride>();
        var page = 1;

        while (true)
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
            var activities = JsonSerializer.Deserialize<List<StravaActivityResponse>>(
                json, _jsonOptions);

            if (activities is null || activities.Count == 0)
                break;

            foreach (var activity in activities)
            {
                if (!IsCyclingActivity(activity)) continue;

                // Only persist rides that have power meter data
                if (activity.AverageWatts <= 0) continue;

                var exists = await _db.Rides.AnyAsync(r => r.Id == activity.Id);
                if (exists) continue;

                newRides.Add(new Ride
                {
                    Id                  = activity.Id,
                    AthleteId           = athleteId,
                    Name                = activity.Name,
                    DistanceMeters      = activity.Distance,
                    MovingTimeSeconds   = activity.MovingTime,
                    ElevationGainMeters = activity.TotalElevationGain,
                    AveragePowerWatts   = activity.AverageWatts,
                    MaxPowerWatts       = activity.MaxWatts,
                    AverageHeartRate    = activity.AverageHeartrate,
                    MaxHeartRate        = activity.MaxHeartrate,
                    AverageSpeedMs      = activity.AverageSpeed,
                    WeightKgAtTime      = athlete.WeightKg,
                    StartDate           = activity.StartDate,
                });
            }

            if (activities.Count < PageSize)
                break;

            page++;
        }

        return newRides;
    }

    // ------------------------------------------------------------------
    // Phase 2 — power stream backfill
    // ------------------------------------------------------------------

    /// <summary>
    /// For each ride belonging to <paramref name="athleteId"/> that has a
    /// recorded average power but no stream entry yet, call the Strava
    /// Streams API and persist the second-by-second watts array.
    /// </summary>
    /// <returns>Number of streams successfully saved.</returns>
    private async Task<int> FetchMissingPowerStreamsAsync(long athleteId)
    {
        // Find rides with power that are missing a stream record.
        // We LEFT JOIN via the navigation table and filter for nulls.
        var ridesNeedingStreams = await _db.Rides
            .Where(r => r.AthleteId == athleteId && r.AveragePowerWatts > 0)
            .Where(r => !_db.RidePowerStreams.Any(s => s.RideId == r.Id))
            .Select(r => r.Id)
            .ToListAsync();

        if (ridesNeedingStreams.Count == 0)
            return 0;

        _logger.LogInformation(
            "Fetching power streams for {Count} rides (athlete {AthleteId})",
            ridesNeedingStreams.Count, athleteId);

        var saved = 0;

        foreach (var rideId in ridesNeedingStreams)
        {
            var stream = await FetchPowerStreamAsync(rideId);
            if (stream is null)
                continue;

            _db.RidePowerStreams.Add(stream);
            await _db.SaveChangesAsync();
            saved++;

            // Respect Strava's rate limit between requests
            await Task.Delay(StreamFetchDelay);
        }

        _logger.LogInformation(
            "Saved {Saved}/{Total} power streams for athlete {AthleteId}",
            saved, ridesNeedingStreams.Count, athleteId);

        return saved;
    }

    /// <summary>
    /// Calls <c>GET /api/v3/activities/{id}/streams?keys=watts&amp;key_by_type=true</c>
    /// and returns a <see cref="RidePowerStream"/> or <c>null</c> on failure.
    /// </summary>
    private async Task<RidePowerStream?> FetchPowerStreamAsync(long rideId)
    {
        var url = $"https://www.strava.com/api/v3/activities/{rideId}/streams" +
                  "?keys=watts&key_by_type=true";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP error fetching power stream for ride {RideId}", rideId);
            return null;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Activity exists but has no streams — not an error, just skip
            _logger.LogDebug("No streams available for ride {RideId}", rideId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Power stream fetch failed for ride {RideId}: {Status}",
                rideId, response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        StravaStreamResponse? streamResponse;

        try
        {
            streamResponse = JsonSerializer.Deserialize<StravaStreamResponse>(
                json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialise stream response for ride {RideId}", rideId);
            return null;
        }

        var wattsData = streamResponse?.Watts?.Data;
        if (wattsData is null || wattsData.Count == 0)
        {
            _logger.LogDebug("Empty watts stream for ride {RideId}", rideId);
            return null;
        }

        return new RidePowerStream
        {
            RideId     = rideId,
            WattsJson  = JsonSerializer.Serialize(wattsData),
            DataPoints = wattsData.Count,
            FetchedAt  = DateTime.UtcNow,
        };
    }

    private static bool IsCyclingActivity(StravaActivityResponse activity) =>
        _cyclingTypes.Contains(activity.SportType) || _cyclingTypes.Contains(activity.Type);
}

/// <summary>Summary of a single ingest run.</summary>
public record IngestResult(
    bool Success,
    int NewRidesCount,
    int NewStreamsCount,
    string? Error);
