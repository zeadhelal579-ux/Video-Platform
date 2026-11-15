// File: JwtOptions.cs
// Purpose: Binds the "Jwt" section of appsettings.json.
namespace VideoPlatform.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // In production this MUST come from a secret manager / environment
    // variable, never committed to appsettings.json as a real value.
    public string SigningKey { get; set; } = string.Empty;

    // Short-lived on purpose (FLOW.md step 5: "typically short-lived, e.g. 15-60 minutes").
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}
