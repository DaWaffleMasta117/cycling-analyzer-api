using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Extensions;
using CyclingAnalyzer.Api.Services;

namespace CyclingAnalyzer.Api.Controllers;

[ApiController]
[Route("api/rides")]
[Authorize] // every endpoint in this controller requires a valid JWT
public class RidesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RideIngestionService _ingestion;

    public RidesController(AppDbContext db, RideIngestionService ingestion)
    {
        _db = db;
        _ingestion = ingestion;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        // AthleteId now comes from the JWT — not the URL
        var athleteId = User.GetAthleteId();
        var result = await _ingestion.IngestRidesAsync(athleteId);

        if (!result.Success)
            return StatusCode(502, new { error = result.Error });

        return Ok(new
        {
            message = "Sync complete",
            newRidesCount = result.NewRidesCount,
            newStreamsCount = result.NewStreamsCount,
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetRides([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var athleteId = User.GetAthleteId();

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