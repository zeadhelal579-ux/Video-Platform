// File: Program.cs
// Purpose: Registers JWT authentication middleware, calls
// AddInfrastructure(), maps the endpoint groups, configures CORS for the
// frontend's origin (ARCHITECTURE.md section 2.4) — plus the production
// concerns: reverse-proxy headers, rate limiting on credential endpoints,
// a global exception handler, health probes, and schema creation at startup.
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using VideoPlatform.Api.Endpoints;
using VideoPlatform.Api.Extensions;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Application.Videos.Commands.ConfirmUpload;
using VideoPlatform.Application.Videos.Commands.RequestUploadUrl;
using VideoPlatform.Application.Videos.Queries.GetVideo;
using VideoPlatform.Application.Videos.Queries.ListVideos;
using VideoPlatform.Domain.Common;
using VideoPlatform.Infrastructure;
using VideoPlatform.Infrastructure.Auth;
using VideoPlatform.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---- Infrastructure: DB, Identity, Storage, Messaging ----
// The Api only ever SENDS messages (ConfirmUploadHandler), so it registers
// no MassTransit consumers — those parameters are left null.
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Application use-case handlers ----
// One explicit registration per handler, against the closed
// ICommandHandler<,>/IQueryHandler<,> interface it implements. Kept
// explicit rather than pulled in via a mediator library — see
// Application/Common/Messaging/ICommandHandler.cs for the reasoning.
// MarkVideoReadyHandler/MarkVideoFailedHandler are NOT registered here:
// only VideoProcessingConsumer (in the Worker process) ever calls them,
// in-process, per ARCHITECTURE.md section 2.5.
builder.Services.AddScoped<ICommandHandler<RequestUploadUrlCommand, Result<RequestUploadUrlResult>>, RequestUploadUrlHandler>();
builder.Services.AddScoped<ICommandHandler<ConfirmUploadCommand, Result>, ConfirmUploadHandler>();
builder.Services.AddScoped<IQueryHandler<GetVideoQuery, Result<VideoDto>>, GetVideoHandler>();
builder.Services.AddScoped<IQueryHandler<ListVideosQuery, Result<ListVideosResult>>, ListVideosHandler>();

// ---- JWT authentication ----
// Every request's "Authorization: Bearer <token>" header is verified here,
// before any endpoint code runs (FLOW.md step 9).
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ---- CORS ----
// Only the frontend's own origin(s) may call this Api with credentials —
// required for the httpOnly refresh-token cookie to be sent and received.
// This policy is what makes a frontend hosted ELSEWHERE work (for example on
// Vercel calling this Api directly), and local development (Vite on :5173).
// When something proxies /api on the frontend's own origin (nginx, or the
// "/api" rewrite in the frontend's vercel.json) browser requests are
// same-origin and CORS never comes into play. Several origins can be given,
// comma-separated, e.g. "https://app.example.com,https://myapp.vercel.app".
// Wildcards are deliberately not supported: with credentials allowed, a
// pattern such as *.vercel.app would let ANY site hosted there act on behalf
// of a signed-in visitor. Vercel preview deployments each have their own URL,
// so they need to be listed explicitly (or use the same-origin rewrite setup).
const string FrontendCorsPolicy = "FrontendCorsPolicy";
var frontendOrigins = (builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    // A pasted "https://app.vercel.app/" must still match the browser's Origin
    // header, which never carries a trailing slash.
    .Select(origin => origin.TrimEnd('/'))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(frontendOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ---- Refresh-cookie settings ----
// SameSite mode of the refresh cookie (Auth__RefreshCookieSameSite):
// Strict by default, None for a frontend on a different site. See AuthCookieOptions.
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));

// ---- Reverse proxy ----
// In production the Api sits behind our own proxy (Caddy in the compose
// setup). Honoring X-Forwarded-For/-Proto makes Request.IsHttps (the refresh
// cookie's Secure flag) and the client IP (rate limiting) reflect the real
// browser connection instead of the proxy hop. Every proxy address is trusted
// because the Api port is never published to the outside — only reachable
// from inside the Docker network (or from the host's loopback). Do not expose
// the Api port directly to the internet with this setting.
// Note for the Vercel "/api" rewrite setup: every request then reaches the Api
// from Vercel's servers, so the rate limiter below sees Vercel's address
// instead of each visitor's; raise RateLimiting__AuthPerMinute accordingly.
// Account lockout (Identity) still limits password guessing per account.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---- Rate limiting ----
// Slows down password guessing and mass registration. Only endpoints that
// opt in via RequireRateLimiting (login, register) are limited.
var authRequestsPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 20);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            "Too many attempts. Please wait a minute and try again.", cancellationToken);
    };

    options.AddPolicy(AuthEndpoints.RateLimitPolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authRequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// ---- Fail fast on an unsafe configuration ----
// Checked after Build() on purpose: design-time tooling ("dotnet ef") builds
// the host without running this code, so it never needs real secrets.
if (jwtOptions.SigningKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 characters. Set it with the Jwt__SigningKey environment variable.");

if (!app.Environment.IsDevelopment() && jwtOptions.SigningKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(
        "Jwt:SigningKey still has the placeholder value. Set a random secret with the Jwt__SigningKey environment variable.");

// ---- Database schema ----
// Creates the schema (or applies migrations) before the first request, so a
// fresh deployment works without a manual step. See DatabaseInitializer.
if (app.Configuration.GetValue("Database:AutoInitialize", true))
    await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);

// ---- Middleware pipeline (order matters) ----
app.UseApiExceptionHandler();
app.UseForwardedHeaders();
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapVideoEndpoints();

app.Run();

// Exposed so VideoPlatform.Tests can spin this Api up with WebApplicationFactory<Program>.
public partial class Program
{
}
