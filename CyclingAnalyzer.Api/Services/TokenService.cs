using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Models;
using CyclingAnalyzer.Api.Settings;

namespace CyclingAnalyzer.Api.Services;

public class TokenService
{
    private readonly AppDbContext _db;
    private readonly StravaSettings _strava;
    private readonly HttpClient _http;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        AppDbContext db,
        IOptions<StravaSettings> strava,
        IHttpClientFactory httpFactory,
        ILogger<TokenService> logger)
    {
        _db     = db;
        _strava = strava.Value;
        _http   = httpFactory.CreateClient("strava");
        _logger = logger;
    }

    // Returns a valid access token for the athlete, refreshing if needed
    public async Task<string?> GetValidTokenAsync(long athleteId)
    {
        var token = await _db.AthleteTokens.FirstOrDefaultAsync(t => t.AthleteId == athleteId);

        if (token is null)
        {
            _logger.LogWarning("No token found for athlete {AthleteId}", athleteId);
            return null;
        }

        // Refresh if token expires within the next 5 minutes
        if (token.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Refreshing token for athlete {AthleteId}", athleteId);

            var refreshRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"]     = _strava.ClientId,
                ["client_secret"] = _strava.ClientSecret,
                ["refresh_token"] = token.RefreshToken,
                ["grant_type"]    = "refresh_token",
            });

            var response = await _http.PostAsync(
                "https://www.strava.com/oauth/token", refreshRequest);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token refresh failed for athlete {AthleteId}", athleteId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var refreshed = JsonSerializer.Deserialize<StravaTokenResponse>(json);

            if (refreshed is null) return null;

            token.AccessToken = refreshed.AccessToken;
            token.RefreshToken = refreshed.RefreshToken;
            token.ExpiresAt   = DateTimeOffset.FromUnixTimeSeconds(refreshed.ExpiresAt).UtcDateTime;
            token.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        return token.AccessToken;
    }
}