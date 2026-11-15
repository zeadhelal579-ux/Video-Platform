// File: MarkVideoReadyHandler.cs
// Purpose: Writes one VideoRendition row per quality, marks the Video
// Ready and its ProcessingJob Succeeded. Called by VideoProcessingConsumer
// IN-PROCESS — Worker and Api share this same Application project, so no
// HTTP hop is needed here (ARCHITECTURE.md section 2.5).
namespace VideoPlatform.Application.Videos.Commands.MarkVideoReady;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;

public sealed class MarkVideoReadyHandler(
    IAppDbContext db,
    IDateTimeProvider clock) : ICommandHandler<MarkVideoReadyCommand, Result>
{
    public async Task<Result> Handle(MarkVideoReadyCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Renditions.Count == 0)
            return Result.Failure("At least one rendition is required to mark a video Ready.");

        var video = await db.Videos.FindAsync([command.VideoId], cancellationToken);
        if (video is null)
            return Result.Failure("Video not found.");

        // Checked up front (instead of letting MarkReady() throw after the
        // rendition rows were already added) so a duplicate delivery is a clean no-op.
        if (video.Status != VideoStatus.Processing)
            return Result.Failure($"Video is {video.Status}, not Processing; it cannot be marked Ready.");

        // The most recent attempt for this video is the one that just finished.
        var job = await db.ProcessingJobs
            .Where(j => j.VideoId == video.Id)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var rendition in command.Renditions)
        {
            db.VideoRenditions.Add(VideoRendition.Create(
                Guid.NewGuid(), video.Id, rendition.Resolution, rendition.M3u8Path, rendition.Bitrate, clock.UtcNow));
        }

        video.MarkReady();
        job?.MarkSucceeded(clock.UtcNow);

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
