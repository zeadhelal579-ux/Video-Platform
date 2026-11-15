// File: ClaimsPrincipalExtensions.cs
// Purpose: Reads the authenticated user's Id out of the JWT's "sub" claim.
// Centralized here so every endpoint that needs the caller's identity gets
// it the exact same way (FLOW.md step 10: "the request proceeds to the
// endpoint... which can now check User.Id against a video's OwnerId").
namespace VideoPlatform.Api.Extensions;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("The current user has no 'sub' claim.");

        return Guid.Parse(value);
    }
}
