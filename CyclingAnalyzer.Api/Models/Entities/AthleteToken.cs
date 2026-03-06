namespace CyclingAnalyzer.Api.Models.Entities;

public class AthleteToken
{
    public int Id { get; set; }
    public long AthleteId { get; set; }        // foreign key
    public string AccessToken { get; set; }  = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; }  = DateTime.UtcNow;

    // Navigation property back to athlete
    public Athlete? Athlete { get; set; }
}