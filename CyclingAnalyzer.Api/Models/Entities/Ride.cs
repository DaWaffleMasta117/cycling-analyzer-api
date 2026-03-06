namespace CyclingAnalyzer.Api.Models.Entities;

public class Ride
{
    public long Id { get; set; } // Strava activity ID
    public long AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float DistanceMeters { get; set; }
    public int MovingTimeSeconds { get; set; }
    public float ElevationGainMeters { get; set; }
    public float AveragePowerWatts { get; set; }
    public float MaxPowerWatts { get; set; }
    public float AverageHeartRate { get; set; }
    public float MaxHeartRate { get; set; }
    public float AverageSpeedMs { get; set; }
    public float WeightKgAtTime { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Athlete? Athlete { get; set; }
}