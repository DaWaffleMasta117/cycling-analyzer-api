using System.Security.Claims;

namespace CyclingAnalyzer.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static long GetAthleteId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue("athleteId");
        if (claim is null)
            throw new InvalidOperationException("athleteId claim not found in token.");

        return long.Parse(claim);
    }
}