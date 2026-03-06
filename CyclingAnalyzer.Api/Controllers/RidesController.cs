using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Services;

namespace CyclingAnalyzer.Api.Controllers;

[ApiController]
[Route("api/rides")]
public class RidesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RideIngestionService _ingestion;

    public RidesController(AppDbContext db, RideIngestionService ingestion)
    {
        _db = db;
        _ingestion = ingestion;
    }

    // Trigger a sync for an athlete
    [HttpPost("sync/{athleteId}")]
    public async Task<IActionResult> Sync(long athleteId)
    {
        // Verify athlete exists
        var athlete = await _db.Athletes.FindAsync(athleteId);
        if (athlete is null)
            return NotFound($"Athlete {athleteId} not found in database.");

        // Verify token exists
        var token = await _db.AthleteTokens
            .FirstOrDefaultAsync(t => t.AthleteId == athleteId);
        if (token is null)
            return NotFound($"No token found for athlete {athleteId}.");

        // Show token status before trying to use it
        var tokenStatus = new
        {
            hasToken     = true,
            expiresAt    = token.ExpiresAt,
            isExpired    = token.ExpiresAt <= DateTime.UtcNow,
            updatedAt    = token.UpdatedAt,
        };

        var result = await _ingestion.IngestRidesAsync(athleteId);

        if (!result.Success)
            return StatusCode(502, new { error = result.Error, tokenStatus });

        return Ok(new
        {
            message       = "Sync complete",
            newRidesCount = result.NewRidesCount,
            tokenStatus,
        });
    }
    // Get all rides for an athlete
    [HttpGet("{athleteId}")]
    public async Task<IActionResult> GetRides(
        long athleteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var rides = await _db.Rides
            .Where(r => r.AthleteId == athleteId)
            .OrderByDescending(r => r.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.DistanceMeters,
                r.MovingTimeSeconds,
                r.ElevationGainMeters,
                r.AveragePowerWatts,
                r.MaxPowerWatts,
                r.AverageHeartRate,
                r.StartDate,
            })
            .ToListAsync();

        return Ok(rides);
    }
}