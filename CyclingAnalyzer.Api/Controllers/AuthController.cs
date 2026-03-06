using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using CyclingAnalyzer.Api.Models;
using CyclingAnalyzer.Api.Settings;

namespace CyclingAnalyzer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly StravaSettings _strava;
    private readonly HttpClient _http;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptions<StravaSettings> strava,
        IHttpClientFactory httpFactory,
        ILogger<AuthController> logger)
    {
        _strava = strava.Value;
        _http   = httpFactory.CreateClient("strava");
        _logger = logger;
    }

    // Step 1 of OAuth — redirect the user to Strava's login page
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

    // Step 2 of OAuth — Strava redirects back here with a code
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Strava auth denied: {Error}", error);
            return BadRequest($"Strava authorisation denied: {error}");
        }

        if (string.IsNullOrEmpty(code))
            return BadRequest("No authorisation code received from Strava.");

        // Exchange the code for real access + refresh tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = _strava.ClientId,
            ["client_secret"] = _strava.ClientSecret,
            ["code"]          = code,
            ["grant_type"]    = "authorization_code",
        });

        var response = await _http.PostAsync("https://www.strava.com/oauth/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed: {Status}", response.StatusCode);
            return StatusCode(502, "Failed to exchange token with Strava.");
        }

        var json   = await response.Content.ReadAsStringAsync();
        var tokens = JsonSerializer.Deserialize<StravaTokenResponse>(json);

        if (tokens is null)
            return StatusCode(502, "Invalid token response from Strava.");

        // TODO: persist tokens to database against athlete ID
        // For now log success and return the token so you can verify it works
        _logger.LogInformation(
            "Auth success for athlete {Id} — {First} {Last}",
            tokens.Athlete?.Id,
            tokens.Athlete?.FirstName,
            tokens.Athlete?.LastName);

        return Ok(new
        {
            message      = "Authentication successful",
            athleteId    = tokens.Athlete?.Id,
            firstName    = tokens.Athlete?.FirstName,
            accessToken  = tokens.AccessToken,   // remove this once you have a DB
            expiresAt    = tokens.ExpiresAt,
        });
    }

    // Step 3 — refresh an expired access token
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