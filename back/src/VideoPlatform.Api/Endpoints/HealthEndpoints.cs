// File: HealthEndpoints.cs
// Purpose: Liveness and readiness probes for Docker, load balancers and
// uptime monitors. Both are anonymous and live under /api/ so they are also
// reachable through the web container's reverse proxy.
namespace VideoPlatform.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Infrastructure.Persistence;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health").WithTags("Health");

        // Liveness: the process is up and answering HTTP.
        group.MapGet("/", () => Results.Ok(new { status = "ok" }));

        // Readiness: additionally proves the database accepts connections.
        group.MapGet("/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
            await db.Database.CanConnectAsync(cancellationToken)
                ? Results.Ok(new { status = "ready" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
    }
}
