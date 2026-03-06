namespace CyclingAnalyzer.Api.Models.Entities;

public class Athlete
{
    public long Id { get; set; }           // Strava athlete ID
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; }  = string.Empty;
    public float WeightKg { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — one athlete has one token
    public AthleteToken? Token { get; set; }
}