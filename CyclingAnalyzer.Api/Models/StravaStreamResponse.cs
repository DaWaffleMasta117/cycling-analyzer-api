using System.Text.Json.Serialization;

namespace CyclingAnalyzer.Api.Models;

/// <summary>
/// Deserialises the response from
///   GET /api/v3/activities/{id}/streams?keys=watts&amp;key_by_type=true
///
/// When key_by_type=true Strava returns a JSON object keyed by stream type,
/// so "watts" is a top-level property containing the stream data.
/// </summary>
public class StravaStreamResponse
{
    [JsonPropertyName("watts")]
    public StravaStreamData? Watts { get; set; }
}

public class StravaStreamData
{
    /// <summary>
    /// One value per second of the activity.  Null means no power reading
    /// was available for that second.
    /// </summary>
    [JsonPropertyName("data")]
    public List<int?> Data { get; set; } = new();

    /// <summary>Total number of data points before any downsampling.</summary>
    [JsonPropertyName("original_size")]
    public int OriginalSize { get; set; }

    /// <summary>Resolution hint from Strava ("high", "medium", "low").</summary>
    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = string.Empty;
}
