using System.Text.Json.Serialization;

namespace CyclingAnalyzer.Api.Models;

public class StravaAthlete
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("firstname")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastname")]
    public string LastName { get; init; } = string.Empty;

    [JsonPropertyName("weight")]
    public float Weight { get; init; }
}