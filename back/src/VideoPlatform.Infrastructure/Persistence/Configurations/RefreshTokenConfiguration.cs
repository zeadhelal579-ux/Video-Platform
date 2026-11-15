// File: RefreshTokenConfiguration.cs
// Purpose: EF Core Fluent API configuration for RefreshToken. See
// Domain/Entities/RefreshToken.cs for why this entity exists beyond the
// original ARCHITECTURE.md list.
namespace VideoPlatform.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoPlatform.Domain.Entities;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(512);

        // Every refresh request looks a token hash up directly — this
        // unique index is both a performance index and an integrity
        // guarantee (two tokens can never hash-collide into one stored row).
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasIndex(t => t.UserId);
    }
}
