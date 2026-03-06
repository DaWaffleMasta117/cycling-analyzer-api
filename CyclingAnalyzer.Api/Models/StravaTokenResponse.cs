using System.Text.Json.Serialization;

namespace CyclingAnalyzer.Api.Models;

public class StravaTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; init; }

    [JsonPropertyName("athlete")]
    public StravaAthlete? Athlete { get; init; }
}