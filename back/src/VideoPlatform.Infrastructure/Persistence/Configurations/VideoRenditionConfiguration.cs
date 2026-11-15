// File: VideoRenditionConfiguration.cs
// Purpose: EF Core Fluent API configuration for VideoRendition.
namespace VideoPlatform.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoPlatform.Domain.Entities;

public sealed class VideoRenditionConfiguration : IEntityTypeConfiguration<VideoRendition>
{
    public void Configure(EntityTypeBuilder<VideoRendition> builder)
    {
        builder.ToTable("VideoRenditions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Resolution)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(r => r.M3u8Path)
            .IsRequired()
            .HasMaxLength(1024);

        builder.HasIndex(r => r.VideoId);

        builder.HasOne<Video>()
            .WithMany()
            .HasForeignKey(r => r.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
