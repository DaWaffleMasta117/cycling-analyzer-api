namespace CyclingAnalyzer.Api.Settings;

public class JwtSettings
{
    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryHours { get; init; } = 24;
}