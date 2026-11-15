// File: AppDbContext.cs
// Purpose: The EF Core DbContext, implementing IAppDbContext. This is the
// only file in the entire backend that "knows" it's PostgreSQL — every
// other layer only ever sees the IAppDbContext interface
// (ARCHITECTURE.md section 2.3).
// Logic/Approach: Extends IdentityDbContext so ASP.NET Core Identity's own
// tables (AspNetUsers, AspNetRoles, ...) are created in the same database,
// through the same migrations pipeline, as the platform's own tables.
namespace VideoPlatform.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Infrastructure.Identity;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<VideoRendition> VideoRenditions => Set<VideoRendition>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // IAppDbContext.SaveChangesAsync(CancellationToken) is already satisfied
    // by DbContext's own SaveChangesAsync — no extra override needed.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own entity configuration must run first when extending
        // IdentityDbContext, then every IEntityTypeConfiguration in this
        // assembly (see Configurations/*.cs) is applied on top of it.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
