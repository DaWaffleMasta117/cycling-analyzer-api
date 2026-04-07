namespace CyclingAnalyzer.Api.Models.Entities;

/// <summary>
/// Stores second-by-second power (watts) data for a ride fetched from the
/// Strava Streams API.  Only created for rides that have a power meter
/// (AveragePowerWatts > 0).
///
/// WattsJson is a JSON-serialised int?[] where each element corresponds to
/// one second of the ride.  Null elements mean no power reading was recorded
/// for that second.  The Rust metrics service reads this table directly.
/// </summary>
public class RidePowerStream
{
    /// <summary>Strava activity ID — also the primary key (1-to-1 with Ride).</summary>
    public long RideId { get; set; }

    /// <summary>
    /// JSON array of nullable ints, e.g. [250,260,null,280,…].
    /// Each index is one second into the ride.
    /// </summary>
    public string WattsJson { get; set; } = "[]";

    /// <summary>Total number of data points (seconds) in the stream.</summary>
    public int DataPoints { get; set; }

    /// <summary>UTC timestamp when this record was fetched from Strava.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Ride? Ride { get; set; }
}
