using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using CyclingAnalyzer.Api.Models;
using CyclingAnalyzer.Api.Models.Entities;
using CyclingAnalyzer.Api.Settings;
using CyclingAnalyzer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CyclingAnalyzer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly StravaSettings _strava;
    private readonly HttpClient _http;
    private readonly ILogger<AuthController> _logger;
    private readonly AppDbContext _db;

    public AuthController(
        IOptions<StravaSettings> strava,
        IHttpClientFactory httpFactory,
        ILogger<AuthController> logger,
        AppDbContext db)
    {
        _strava = strava.Value;
        _http   = httpFactory.CreateClient("strava");
        _logger = logger;
        _db     = db;
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
            "&scope=read,activity:read_all";

        return Redirect(stravaAuthUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
            return BadRequest($"Strava authorisation denied: {error}");

        if (string.IsNullOrEmpty(code))
            return BadRequest("No authorisation code received from Strava.");

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = _strava.ClientId,
            ["client_secret"] = _strava.ClientSecret,
            ["code"]          = code,
            ["grant_type"]    = "authorization_code",
        });

        var response = await _http.PostAsync("https://www.strava.com/oauth/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
            return StatusCode(502, "Failed to exchange token with Strava.");

        var json   = await response.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<StravaTokenResponse>(json);

        if (tokens is null || tokens.Athlete is null)
            return StatusCode(502, "Invalid token response from Strava.");

        // Upsert athlete — update if exists, insert if new
        var athlete = await _db.Athletes.FindAsync(tokens.Athlete.Id);
        if (athlete is null)
        {
            athlete = new Athlete
            {
                Id        = tokens.Athlete.Id,
                FirstName = tokens.Athlete.FirstName,
                LastName  = tokens.Athlete.LastName,
                WeightKg  = tokens.Athlete.Weight,
            };
            _db.Athletes.Add(athlete);
        }
        else
        {
            athlete.FirstName = tokens.Athlete.FirstName;
            athlete.LastName  = tokens.Athlete.LastName;
            athlete.WeightKg  = tokens.Athlete.Weight;
            athlete.UpdatedAt = DateTime.UtcNow;
        }

        // Upsert token
        var existing = await _db.AthleteTokens.FirstOrDefaultAsync(t => t.AthleteId == tokens.Athlete.Id);

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(tokens.ExpiresAt).UtcDateTime;

        if (existing is null)
        {
            _db.AthleteTokens.Add(new AthleteToken
            {
                AthleteId    = tokens.Athlete.Id,
                AccessToken  = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt    = expiresAt,
            });
        }
        else
        {
            existing.AccessToken  = tokens.AccessToken;
            existing.RefreshToken = tokens.RefreshToken;
            existing.ExpiresAt    = expiresAt;
            existing.UpdatedAt    = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Auth success for athlete {Id} — {First} {Last}",
            tokens.Athlete.Id,
            tokens.Athlete.FirstName,
            tokens.Athlete.LastName);

        return Ok(new
        {
            message   = "Authentication successful",
            athleteId = tokens.Athlete.Id,
            firstName = tokens.Athlete.FirstName,
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken)
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = _strava.ClientId,
            ["client_secret"] = _strava.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"]    = "refresh_token",
        });

        var response = await _http.PostAsync("https://www.strava.com/oauth/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
            return StatusCode(502, "Token refresh failed.");

        var json   = await response.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<StravaTokenResponse>(json);

        return Ok(new
        {
            accessToken = tokens?.AccessToken,
            expiresAt   = tokens?.ExpiresAt,
        });
    }
}