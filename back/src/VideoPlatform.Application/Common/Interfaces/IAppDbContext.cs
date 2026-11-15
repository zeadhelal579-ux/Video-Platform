// File: IAppDbContext.cs
// Purpose: Abstracts the EF Core DbContext behind an interface so every
// Application handler depends on THIS, never on the concrete EF Core class
// in Infrastructure — handlers can then be unit-tested against an
// in-memory fake instead of a real PostgreSQL database.
// Logic/Approach: Exposes DbSet<T> directly rather than one repository
// interface per entity, because EF Core's DbSet already behaves like a
// repository — an extra layer on top would be indirection without value
// (ARCHITECTURE.md section 2.2).
// Note: Domain.User is deliberately NOT exposed here. User accounts are
// owned and queried through ASP.NET Core Identity's UserManager in the Api
// layer instead, so Identity keeps full, unbroken control over password
// hashing and account security — see the final report for the full
// reasoning behind this one clarification to the original design.
namespace VideoPlatform.Application.Common.Interfaces;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Domain.Entities;

public interface IAppDbContext
{
    DbSet<Video> Videos { get; }
    DbSet<ProcessingJob> ProcessingJobs { get; }
    DbSet<VideoRendition> VideoRenditions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
