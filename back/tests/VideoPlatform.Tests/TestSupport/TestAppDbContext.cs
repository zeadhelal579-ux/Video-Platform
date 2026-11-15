// File: TestAppDbContext.cs
// Purpose: A lightweight, Identity-free DbContext used only by tests,
// backed by EF Core's InMemory provider. Implements the exact same
// IAppDbContext interface every Application handler depends on — this is
// precisely the payoff that interface exists for (ARCHITECTURE.md section
// 2.2: "so they can be unit-tested with an in-memory fake").
namespace VideoPlatform.Tests.TestSupport;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Domain.Entities;

public sealed class TestAppDbContext(DbContextOptions<TestAppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<VideoRendition> VideoRenditions => Set<VideoRendition>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Minimal key configuration only — no PostgreSQL-only details
        // (string conversions, max lengths) and no Identity tables, since
        // InMemory doesn't need the former and these tests never touch the latter.
        modelBuilder.Entity<Video>().HasKey(v => v.Id);
        modelBuilder.Entity<ProcessingJob>().HasKey(j => j.Id);
        modelBuilder.Entity<VideoRendition>().HasKey(r => r.Id);
        modelBuilder.Entity<RefreshToken>().HasKey(t => t.Id);
    }
}

public static class TestAppDbContextFactory
{
    // A fresh, isolated in-memory database per test — the random GUID name
    // is what guarantees tests never see each other's data.
    public static TestAppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }
}
