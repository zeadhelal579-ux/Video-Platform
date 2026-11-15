// File: ConfirmUploadHandler.cs
// Purpose: Moves the video to Queued, records a new ProcessingJob attempt,
// and publishes VideoProcessingRequested (FLOW.md, Video Upload Flow, step 8).
namespace VideoPlatform.Application.Videos.Commands.ConfirmUpload;

using VideoPlatform.Application.Common.Contracts;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;

public sealed class ConfirmUploadHandler(
    IAppDbContext db,
    IVideoProcessingPublisher publisher,
    IDateTimeProvider clock) : ICommandHandler<ConfirmUploadCommand, Result>
{
    public async Task<Result> Handle(ConfirmUploadCommand command, CancellationToken cancellationToken = default)
    {
        var video = await db.Videos.FindAsync([command.VideoId], cancellationToken);
        if (video is null)
            return Result.Failure("Video not found.");

        if (video.OwnerId != command.RequesterId)
            return Result.Failure("You do not have permission to confirm this upload.");

        // A second confirm (double click, a retry after a network timeout)
        // must not queue the same video twice. That is an expected,
        // user-visible situation, so it is a Result failure — not an exception.
        if (video.Status != VideoStatus.Pending)
            return Result.Failure("This upload was already confirmed.");

        video.MarkQueued();

        var job = ProcessingJob.Create(Guid.NewGuid(), video.Id);
        db.ProcessingJobs.Add(job);

        await db.SaveChangesAsync(cancellationToken);

        // Published only AFTER SaveChangesAsync succeeds, so a save that
        // never happened can never look Queued. That still leaves one gap:
        // the DB write and the broker publish are two separate operations,
        // not one atomic transaction (that needs a transactional outbox,
        // which this project does not have), so if THIS call throws after
        // the save above already succeeded, the video would otherwise be
        // stuck at Queued forever: no message ever reaches the Worker, and
        // MarkQueued() only accepts Pending -> Queued, so confirming again
        // would also fail. Catch that specific window and turn it into an
        // honest, visible Failed state instead of a silent stall.
        try
        {
            await publisher.PublishAsync(new VideoProcessingRequested(video.Id, video.OriginalStoragePath), cancellationToken);
        }
        catch (Exception ex)
        {
            video.MarkFailed();
            job.MarkFailed($"Failed to publish the processing job: {ex.Message}", clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Failure("Upload confirmed, but the video could not be queued for processing. Please try again.");
        }

        return Result.Success();
    }
}
