using System.Text.Json.Serialization;

namespace CyclingAnalyzer.Api.Models;

public class StravaActivityResponse
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("distance")]
    public float Distance { get; init; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; init; }

    [JsonPropertyName("total_elevation_gain")]
    public float TotalElevationGain { get; init; }

    [JsonPropertyName("average_watts")]
    public float AverageWatts { get; init; }

    [JsonPropertyName("max_watts")]
    public float MaxWatts { get; init; }

    [JsonPropertyName("average_heartrate")]
    public float AverageHeartrate { get; init; }

    [JsonPropertyName("max_heartrate")]
    public float MaxHeartrate { get; init; }

    [JsonPropertyName("average_speed")]
    public float AverageSpeed { get; init; }

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sport_type")]
    public string SportType { get; init; } = string.Empty;
}