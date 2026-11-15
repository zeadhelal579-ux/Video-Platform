// File: ProcessingJobConfiguration.cs
// Purpose: EF Core Fluent API configuration for ProcessingJob.
namespace VideoPlatform.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoPlatform.Domain.Entities;

public sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        builder.ToTable("ProcessingJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(ProcessingJob.MaxErrorMessageLength);

        // A video can have several jobs (one per attempt) — this index keeps
        // "find the most recent attempt for this video" fast, which is what
        // MarkVideoReadyHandler and MarkVideoFailedHandler both do.
        builder.HasIndex(j => j.VideoId);

        builder.HasOne<Video>()
            .WithMany()
            .HasForeignKey(j => j.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
