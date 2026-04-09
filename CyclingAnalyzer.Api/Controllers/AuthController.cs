using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using CyclingAnalyzer.Api.Data;
using CyclingAnalyzer.Api.Models;
using CyclingAnalyzer.Api.Models.Entities;
using CyclingAnalyzer.Api.Services;
using CyclingAnalyzer.Api.Settings;

namespace CyclingAnalyzer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly StravaSettings _strava;
    private readonly HttpClient _http;
    private readonly ILogger<AuthController> _logger;
    private readonly AppDbContext _db;
    private readonly JwtService _jwtService;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthController(
        IOptions<StravaSettings> strava,
        IHttpClientFactory httpFactory,
        ILogger<AuthController> logger,
        AppDbContext db,
        JwtService jwtService)
    {
        _strava = strava.Value;
        _http = httpFactory.CreateClient("strava");
        _logger = logger;
        _db = db;
        _jwtService = jwtService;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var stravaAuthUrl =
            "https://www.strava.com/oauth/authorize" +
            $"?client_id={_strava.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(_strava.RedirectUri)}" +
            "&response_type=code" +
            "&approval_prompt=auto" +
            "&scope=read,activity:read_all,profile:read_all";

        return Redirect(stravaAuthUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
            return BadRequest($"Strava authorisation denied: {error}");

        if (string.IsNullOrEmpty(code))
            return BadRequest("No authorisation code received from Strava.");

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _strava.ClientId,
            ["client_secret"] = _strava.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        });

        var response = await _http.PostAsync(
            "https://www.strava.com/oauth/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
            return StatusCode(502, "Failed to exchange token with Strava.");

        var json   = await response.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<StravaTokenResponse>(json);

        if (tokens is null || tokens.Athlete is null)
            return StatusCode(502, "Invalid token response from Strava.");

        // The OAuth token exchange returns a SummaryAthlete which does not include
        // the weight field. Fetch the full DetailedAthlete profile to get it.
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        float weightKg = 0f;
        var profileResponse = await _http.GetAsync("https://www.strava.com/api/v3/athlete");
        if (profileResponse.IsSuccessStatusCode)
        {
            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            var detailedAthlete = JsonSerializer.Deserialize<StravaAthlete>(
                profileJson, _jsonOptions);
            weightKg = detailedAthlete?.Weight ?? 0f;
        }
        else
        {
            _logger.LogWarning(
                "Could not fetch detailed athlete profile: {Status}",
                profileResponse.StatusCode);
        }

        // Upsert athlete
        var athlete = await _db.Athletes.FindAsync(tokens.Athlete.Id);
        if (athlete is null)
        {
            athlete = new Athlete
            {
                Id        = tokens.Athlete.Id,
                FirstName = tokens.Athlete.FirstName,
                LastName  = tokens.Athlete.LastName,
                WeightKg  = weightKg,
            };
            _db.Athletes.Add(athlete);
        }
        else
        {
            athlete.FirstName = tokens.Athlete.FirstName;
            athlete.LastName  = tokens.Athlete.LastName;
            if (weightKg > 0) athlete.WeightKg = weightKg;
            athlete.UpdatedAt = DateTime.UtcNow;
        }

        // Upsert token
        var existing = await _db.AthleteTokens
            .FirstOrDefaultAsync(t => t.AthleteId == tokens.Athlete.Id);

        var expiresAt = DateTimeOffset
            .FromUnixTimeSeconds(tokens.ExpiresAt)
            .UtcDateTime;

        if (existing is null)
        {
            _db.AthleteTokens.Add(new AthleteToken
            {
                AthleteId = tokens.Athlete.Id,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = expiresAt,
            });
        }
        else
        {
            existing.AccessToken = tokens.AccessToken;
            existing.RefreshToken = tokens.RefreshToken;
            existing.ExpiresAt = expiresAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Generate JWT for the React frontend to use
        var jwt = _jwtService.GenerateToken(
            tokens.Athlete.Id,
            tokens.Athlete.FirstName,
            tokens.Athlete.LastName);

        // Redirect to React callback page with JWT in query params
        var redirectUrl = $"http://localhost:5173/callback" +
            $"?jwt={jwt}" +
            $"&athleteId={tokens.Athlete.Id}" +
            $"&firstName={Uri.EscapeDataString(tokens.Athlete.FirstName)}";

        return Redirect(redirectUrl);
    }
}