// File: VideoConfiguration.cs
// Purpose: EF Core Fluent API configuration for Video — indexes, required
// fields, string length limits — kept out of the entity class itself so
// Domain stays free of persistence concerns (ARCHITECTURE.md section 2.3).
namespace VideoPlatform.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoPlatform.Domain.Entities;

public sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("Videos");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(v => v.OriginalStoragePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(v => v.ThumbnailPath)
            .HasMaxLength(1024);

        builder.Property(v => v.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        // Every "list my videos" query filters by OwnerId — this index is
        // what keeps that query fast as the table grows.
        builder.HasIndex(v => v.OwnerId);
    }
}
