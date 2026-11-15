// File: AuthCookieOptions.cs
// Purpose: Lets the deployment choose how strictly the browser guards the
// refresh-token cookie, because the right value depends on WHERE the frontend
// is hosted relative to this Api.
// Logic/Approach: Bound from the "Auth" configuration section, i.e. the
// environment variable Auth__RefreshCookieSameSite.
//   Strict (default) — the frontend and the Api are the same site: one origin
//       behind a proxy (nginx, or the Vercel "/api" rewrite), or sibling
//       subdomains such as app.example.com + api.example.com. Safest.
//   Lax — also fine for the cases above; additionally sent on top-level navigations.
//   None — the frontend and the Api are on DIFFERENT sites (for example
//       myapp.vercel.app calling api.other-domain.com directly). The browser
//       only stores such a cookie when it is also Secure, so HTTPS is mandatory,
//       and Safari/iOS block it as a third-party cookie (sessions then end on
//       every page reload) — prefer the same-site or same-origin setups.
// Because None removes the browser's built-in cross-site protection, /refresh
// and /logout additionally demand a custom request header in that mode (see
// AuthEndpoints.PassesCsrfGuard).
namespace VideoPlatform.Api.Endpoints;

public sealed class AuthCookieOptions
{
    // Configuration section name / environment-variable prefix ("Auth__").
    public const string SectionName = "Auth";

    // "Strict", "Lax" or "None" (case-insensitive).
    public string RefreshCookieSameSite { get; set; } = "Strict";

    // Turns the configured text into the framework enum. Anything that is not
    // exactly one of the three values (a typo, an empty string, a number)
    // falls back to Strict, the safest choice.
    public SameSiteMode ResolveSameSite() =>
        Enum.TryParse<SameSiteMode>(RefreshCookieSameSite, ignoreCase: true, out var mode)
        && Enum.IsDefined(mode)
        && mode != SameSiteMode.Unspecified
            ? mode
            : SameSiteMode.Strict;
}
