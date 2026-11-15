// File: AuthEndpoints.cs
// Purpose: /register, /login, /refresh, /logout — backed directly by
// ASP.NET Core Identity's UserManager (ARCHITECTURE.md section 2.4;
// FLOW.md, Authentication & Token Flow, the full round trip).
// Logic/Approach: Auth intentionally does NOT go through the Application
// Command/Query pattern the Videos/ folder uses. These endpoints need
// HttpContext/cookie access, which only this outermost, web-aware layer
// legitimately has — routing them through the framework-agnostic
// Application layer first would add indirection without value. This
// matches FLOW.md's own description: "AuthEndpoints.cs receives it and
// hands off to ASP.NET Core Identity" directly.
// Password checking uses UserManager plus Identity's lockout counters (the
// same rules SignInManager applies) because this Api is stateless: it never
// creates a cookie sign-in session, so SignInManager adds nothing here.
namespace VideoPlatform.Api.Endpoints;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Infrastructure.Auth;
using VideoPlatform.Infrastructure.Identity;

public static class AuthEndpoints
{
    // Name of the rate-limit policy registered in Program.cs. It is applied
    // to the endpoints where guessing credentials pays off (login, register).
    public const string RateLimitPolicyName = "auth";

    private const string RefreshCookieName = "refreshToken";

    // Header the frontend adds to every call (frontend api/client.ts). When the
    // cookie is SameSite=None a plain cross-site request could make the browser
    // attach it; a custom header cannot ride on a cross-site request without a
    // CORS preflight, and only the configured frontend origins pass that.
    private const string CsrfGuardHeaderName = "X-Requested-With";

    // Only /refresh and /logout ever need the cookie, so the browser is told
    // to send it nowhere else (it never rides along on /api/videos calls).
    private const string RefreshCookiePath = "/api/auth";

    // A refresh token that was rotated away only moments ago is still
    // accepted for this long (see RefreshToken.CanBeRotated).
    private static readonly TimeSpan RefreshGraceWindow = TimeSpan.FromSeconds(30);

    private const int MaxEmailLength = 254;
    private const int MaxDisplayNameLength = 100;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register).RequireRateLimiting(RateLimitPolicyName);
        group.MapPost("/login", Login).RequireRateLimiting(RateLimitPolicyName);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);
    }

    // POST /api/auth/register — creates the account. Password hashing is
    // entirely Identity's responsibility (TOOLS.md, "ASP.NET Core Identity").
    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IDateTimeProvider clock)
    {
        // Missing JSON properties arrive as null; catching them here turns what
        // would be a server error (null password, NOT NULL column) into a clean 400.
        var inputErrors = ValidateRegistration(request);
        if (inputErrors.Count > 0)
            return TypedResults.ValidationProblem(inputErrors);

        var email = request.Email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = clock.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        return result.Succeeded
            ? TypedResults.Created($"/api/users/{user.Id}", new { user.Id, user.Email })
            : TypedResults.ValidationProblem(ToValidationErrors(result));
    }

    // POST /api/auth/login — FLOW.md steps 3-6: verify password, issue token pair.
    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtGenerator,
        IAppDbContext db,
        IDateTimeProvider clock,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return TypedResults.Unauthorized();

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return TypedResults.Unauthorized();

        // Account lockout: after too many wrong passwords Identity locks the
        // account for a while. A locked account answers exactly like a wrong
        // password, so the response never reveals which accounts exist or are locked.
        if (await userManager.IsLockedOutAsync(user))
            return TypedResults.Unauthorized();

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return TypedResults.Unauthorized();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await IssueTokenPairAsync(user, httpContext, jwtGenerator, db, clock, jwtOptions.Value, cancellationToken);
    }

    // POST /api/auth/refresh — reads the httpOnly cookie automatically sent
    // by the browser, never a request body (FLOW.md step 12).
    private static async Task<IResult> Refresh(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtGenerator,
        IAppDbContext db,
        IDateTimeProvider clock,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        if (!PassesCsrfGuard(httpContext))
            return TypedResults.Unauthorized();

        if (!httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            return TypedResults.Unauthorized();

        var tokenHash = TokenHasher.Hash(refreshToken);
        var storedToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.CanBeRotated(clock.UtcNow, RefreshGraceWindow))
            return TypedResults.Unauthorized();

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user is null)
            return TypedResults.Unauthorized();

        // Rotation: the token just used is revoked immediately and replaced
        // by a brand new one — this limits a leaked refresh token to a single use.
        storedToken.Revoke();

        return await IssueTokenPairAsync(user, httpContext, jwtGenerator, db, clock, jwtOptions.Value, cancellationToken);
    }

    // POST /api/auth/logout — revokes the refresh token server-side and
    // clears the cookie, so a stolen cookie stops working immediately.
    private static async Task<IResult> Logout(HttpContext httpContext, IAppDbContext db, CancellationToken cancellationToken)
    {
        if (!PassesCsrfGuard(httpContext))
            return TypedResults.Unauthorized();

        if (httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            var tokenHash = TokenHasher.Hash(refreshToken);
            var storedToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
            // Logout must not use the grace window reserved for token rotation.
            if (storedToken is not null)
            {
                db.RefreshTokens.Remove(storedToken);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        // A cookie is only removed when the delete uses the same Path it was set with.
        httpContext.Response.Cookies.Delete(RefreshCookieName, BuildRefreshCookieOptions(httpContext, expires: null));
        return TypedResults.NoContent();
    }

    // Shared by Login and Refresh: builds a new access+refresh pair,
    // persists the refresh token's hash, and sets the httpOnly cookie
    // (FLOW.md steps 6-7).
    private static async Task<IResult> IssueTokenPairAsync(
        ApplicationUser user,
        HttpContext httpContext,
        IJwtTokenGenerator jwtGenerator,
        IAppDbContext db,
        IDateTimeProvider clock,
        JwtOptions jwtOptions,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var (accessToken, expiresAt) = jwtGenerator.GenerateAccessToken(user.Id, user.Email!);
        var refreshToken = jwtGenerator.GenerateRefreshToken();
        var refreshExpiresAt = now.AddDays(jwtOptions.RefreshTokenDays);

        // Housekeeping: this user's expired tokens, and tokens rotated away
        // long enough ago that the grace window no longer applies, are dead
        // weight. Dropping them here keeps the table from growing without
        // bound, with no separate cleanup job to run.
        var rotatedBefore = now - RefreshGraceWindow;
        var staleTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.ExpiresAt < now || t.RevokedAt < rotatedBefore))
            .ToListAsync(cancellationToken);
        db.RefreshTokens.RemoveRange(staleTokens);

        db.RefreshTokens.Add(RefreshToken.Create(
            Guid.NewGuid(), user.Id, TokenHasher.Hash(refreshToken), refreshExpiresAt, now));

        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Cookies.Append(RefreshCookieName, refreshToken, BuildRefreshCookieOptions(httpContext, refreshExpiresAt));

        return TypedResults.Ok(new AuthResponse(accessToken, expiresAt));
    }

    // HttpOnly: JavaScript can never read this cookie (mitigates XSS).
    // SameSite comes from configuration (AuthCookieOptions): Strict by default —
    // the browser then never sends the cookie cross-site (mitigates CSRF, see
    // FLOW.md step 7) — or None when the frontend is hosted on a different
    // site than this Api (e.g. Vercel + a separate API domain). Strict cannot
    // work there: the browser would refuse to store or send the cookie, and
    // every page reload would end the session.
    // Secure is tied to how this very request arrived (after the
    // forwarded-headers middleware has looked at the reverse proxy's
    // X-Forwarded-Proto) rather than hardcoded true: a browser only stores a
    // Secure cookie that was set over HTTPS, and the default local setup runs
    // over plain HTTP. Hardcoding Secure = true would make the browser silently
    // discard the cookie there — login would still return 200, but the very
    // next refresh would find no cookie and log the user straight back out.
    // Behind real HTTPS it is always true. SameSite=None is the exception:
    // browsers reject it without Secure, so it is forced on in that mode.
    private static CookieOptions BuildRefreshCookieOptions(HttpContext httpContext, DateTimeOffset? expires)
    {
        var sameSite = GetCookieSettings(httpContext).ResolveSameSite();

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = sameSite == SameSiteMode.None || httpContext.Request.IsHttps,
            SameSite = sameSite,
            Path = RefreshCookiePath,
            Expires = expires
        };
    }

    // With SameSite=None the browser's own cross-site protection is gone, so
    // the cookie-authenticated endpoints (/refresh, /logout) require the
    // custom header above. In Strict/Lax mode the browser already guarantees
    // the cookie is only sent same-site, so no extra header is demanded.
    private static bool PassesCsrfGuard(HttpContext httpContext) =>
        GetCookieSettings(httpContext).ResolveSameSite() != SameSiteMode.None
        || httpContext.Request.Headers.ContainsKey(CsrfGuardHeaderName);

    // Resolved from the request's service provider so the cookie helpers stay
    // static methods (the endpoint handlers above are static too).
    private static AuthCookieOptions GetCookieSettings(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IOptions<AuthCookieOptions>>().Value;

    // Field-level checks for registration; the keys match the JSON property
    // names the frontend renders errors under.
    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > MaxEmailLength)
            errors["email"] = ["A valid email address is required."];

        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > MaxDisplayNameLength)
            errors["displayName"] = [$"Display name is required and must be at most {MaxDisplayNameLength} characters."];

        if (string.IsNullOrEmpty(request.Password))
            errors["password"] = ["Password is required."];

        return errors;
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);

// The exact shape the frontend receives — matches FLOW.md step 6:
// "{ accessToken, refreshToken }" (the refresh token itself travels only
// via the httpOnly cookie, never in this JSON body).
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt);
