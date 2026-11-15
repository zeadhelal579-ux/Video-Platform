// File: DatabaseInitializer.cs
// Purpose: Brings the database schema up to date when the Api starts, so a
// fresh deployment works without a manual "dotnet ef database update" step.
// Logic/Approach: If the assembly contains EF Core migrations (generated with
// "dotnet ef migrations add"), they are applied. If it contains none yet, the
// schema is created straight from the model with EnsureCreated, which is
// always consistent with the code. Do not mix the two on one database: once
// you start generating migrations, begin from an empty database.
// The Api also retries for a while, because the database container is often
// still booting when the Api starts.
namespace VideoPlatform.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DatabaseInitializer
{
    private const int MaxAttempts = 15;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(4);

    // Creates or migrates the schema, retrying while the database is unreachable.
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (db.Database.GetMigrations().Any())
                {
                    logger.LogInformation("Applying EF Core migrations...");
                    await db.Database.MigrateAsync(cancellationToken);
                }
                else
                {
                    logger.LogInformation("No migrations found; creating the schema from the model (EnsureCreated).");
                    await db.Database.EnsureCreatedAsync(cancellationToken);
                }

                logger.LogInformation("Database is ready.");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}s.",
                    attempt, MaxAttempts, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
