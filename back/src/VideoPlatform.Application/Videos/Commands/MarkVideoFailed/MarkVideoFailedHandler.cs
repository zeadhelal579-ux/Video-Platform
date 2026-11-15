// File: MarkVideoFailedHandler.cs
// Purpose: Marks the Video Failed and its ProcessingJob DeadLettered, once
// retries are exhausted and RabbitMQ has routed the message to its
// Dead-Letter Exchange (FLOW.md, Video Processing Flow, step 7). The video
// is never silently lost — the error stays attached to the job row.
namespace VideoPlatform.Application.Videos.Commands.MarkVideoFailed;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;

public sealed class MarkVideoFailedHandler(
    IAppDbContext db,
    IDateTimeProvider clock) : ICommandHandler<MarkVideoFailedCommand, Result>
{
    public async Task<Result> Handle(MarkVideoFailedCommand command, CancellationToken cancellationToken = default)
    {
        var video = await db.Videos.FindAsync([command.VideoId], cancellationToken);
        if (video is null)
            return Result.Failure("Video not found.");

        // A redelivered or late fault notification must never undo a video that
        // already finished: only one that is waiting or being processed can fail.
        if (video.Status is not (VideoStatus.Queued or VideoStatus.Processing))
            return Result.Failure($"Video is already {video.Status}; it cannot be marked Failed.");

        var job = await db.ProcessingJobs
            .Where(j => j.VideoId == video.Id)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        video.MarkFailed();
        job?.MarkDeadLettered(command.ErrorMessage, clock.UtcNow);

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
