// File: VideoProcessingConsumer.cs
// Purpose: Subscribed to VideoProcessingRequested via MassTransit — this
// is the Worker's entire entry point (ARCHITECTURE.md section 2.5).
// Logic/Approach: Downloads the raw file, transcodes it, uploads every
// HLS output, then calls MarkVideoReadyHandler directly IN-PROCESS (Worker
// and Api share the Application project — FLOW.md, Video Processing Flow,
// step 6). On failure, this attempt's own error is recorded on the
// ProcessingJob row (MarkJobAttemptFailedAsync) before the exception is
// rethrown on purpose: rethrowing is exactly what tells MassTransit's retry
// middleware (Infrastructure/DependencyInjection.cs) to requeue with
// backoff. Once retries are exhausted, MassTransit publishes
// Fault<VideoProcessingRequested>, handled by VideoProcessingFaultConsumer
// (Consumers/VideoProcessingFaultConsumer.cs) — the terminal failure path
// from FLOW.md, Video Processing Flow, step 7.
// Delivery is at-least-once, so the consumer is idempotent: a message for a
// video that is already Ready / Failed / gone is dropped instead of
// re-processed. One attempt is also bounded by a timeout, so a hung FFmpeg
// can never block the queue.
namespace VideoPlatform.Worker.Consumers;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoPlatform.Application.Common.Contracts;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Application.Videos.Commands.MarkVideoReady;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Worker.Services;

public sealed class VideoProcessingConsumer(
    IAppDbContext db,
    IStorageService storage,
    ITranscodingService transcodingService,
    IDateTimeProvider clock,
    ICommandHandler<MarkVideoReadyCommand, Result> markReadyHandler,
    IOptions<WorkerOptions> workerOptions,
    ILogger<VideoProcessingConsumer> logger) : IConsumer<VideoProcessingRequested>
{
    public async Task Consume(ConsumeContext<VideoProcessingRequested> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        logger.LogInformation("Starting processing for video {VideoId}", message.VideoId);

        // Messages are delivered at least once: this one may belong to a video
        // that is already done, already failed for good, or deleted. Returning
        // normally acknowledges (and so discards) it; throwing would only
        // trigger pointless retries.
        if (!await TryMarkJobRunningAsync(message.VideoId, cancellationToken))
            return;

        // Bounds this whole attempt. Cancelled by the timeout OR by the bus
        // shutting down (cancellationToken), whichever comes first.
        var timeout = TimeSpan.FromMinutes(Math.Max(1, workerOptions.Value.TranscodeTimeoutMinutes));
        using var attemptSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptSource.CancelAfter(timeout);
        var attemptToken = attemptSource.Token;

        var workDirectory = Path.Combine(Path.GetTempPath(), "videoplatform", message.VideoId.ToString());
        Directory.CreateDirectory(workDirectory);

        try
        {
            // The local copy gets a fixed name; only a vetted extension of the
            // stored key is reused, so nothing from the key can steer where the file lands.
            var localSourcePath = Path.Combine(workDirectory, "source" + GetSafeExtension(message.OriginalStoragePath));
            await storage.DownloadAsync(message.OriginalStoragePath, localSourcePath, attemptToken);

            var transcodeResult = await transcodingService.TranscodeAsync(
                localSourcePath, Path.Combine(workDirectory, "hls"), attemptToken);

            var renditionInputs = new List<RenditionInput>();

            foreach (var rendition in transcodeResult.Renditions)
            {
                var remoteFolder = $"videos/{message.VideoId}/hls/{ResolutionPreset.FolderNames[rendition.Resolution]}";

                foreach (var segmentPath in rendition.SegmentPaths)
                {
                    await UploadFileAsync(
                        storage, segmentPath, $"{remoteFolder}/{Path.GetFileName(segmentPath)}", "video/mp2t", attemptToken);
                }

                var remoteM3u8Key = $"{remoteFolder}/playlist.m3u8";
                await UploadFileAsync(storage, rendition.LocalM3u8Path, remoteM3u8Key, "application/vnd.apple.mpegurl", attemptToken);

                renditionInputs.Add(new RenditionInput(rendition.Resolution, remoteM3u8Key, rendition.Bitrate));
            }

            var remoteMasterKey = $"videos/{message.VideoId}/hls/master.m3u8";
            await UploadFileAsync(storage, transcodeResult.MasterPlaylistLocalPath, remoteMasterKey, "application/vnd.apple.mpegurl", attemptToken);

            var result = await markReadyHandler.Handle(new MarkVideoReadyCommand(message.VideoId, renditionInputs), cancellationToken);
            if (result.IsFailure)
                logger.LogWarning("MarkVideoReady failed for video {VideoId}: {Error}", message.VideoId, result.Error);

            logger.LogInformation("Finished processing video {VideoId}", message.VideoId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Record what went wrong with *this* attempt before MassTransit
            // retries or dead-letters the message. Without this, only the
            // very last failure reason would ever survive anywhere (via
            // Fault<T> in VideoProcessingFaultConsumer) — every earlier
            // attempt's real error (e.g. attempt 1's transient download
            // timeout) would be silently overwritten by the next retry's
            // TryMarkJobRunningAsync call and lost.
            // The filter above skips a bus shutdown: that is not this video's
            // fault, and the message simply goes back to the queue untouched.
            var reason = attemptSource.IsCancellationRequested
                ? $"Processing timed out after {timeout.TotalMinutes:0} minutes."
                : string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;

            // CancellationToken.None: recording the failure must still work
            // even if the attempt's own token has just been cancelled.
            await MarkJobAttemptFailedAsync(message.VideoId, reason, CancellationToken.None);
            throw; // preserve the original behavior: MassTransit's retry middleware must still see the fault.
        }
        finally
        {
            // Temp files never need to survive past this point — nothing
            // about final state depends on them once uploads have finished.
            TryDeleteDirectory(workDirectory);
        }
    }

    // Moves Video -> Processing and increments this attempt's counter,
    // before any actual work starts (FLOW.md, Video Processing Flow, step 2).
    // Returns false when there is nothing to process (the message is stale).
    private async Task<bool> TryMarkJobRunningAsync(Guid videoId, CancellationToken cancellationToken)
    {
        var video = await db.Videos.FindAsync([videoId], cancellationToken);
        if (video is null)
        {
            logger.LogWarning("Video {VideoId} no longer exists; dropping the message.", videoId);
            return false;
        }

        // Queued = first delivery, Processing = a retry after a failed attempt.
        if (video.Status is not (VideoStatus.Queued or VideoStatus.Processing))
        {
            logger.LogInformation("Video {VideoId} is {Status}, not waiting for processing; dropping the message.", videoId, video.Status);
            return false;
        }

        video.MarkProcessing();

        var job = await db.ProcessingJobs
            .Where(j => j.VideoId == videoId)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        job?.MarkRunning(clock.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Records why this specific attempt failed (ProcessingJob.MarkFailed —
    // Domain/Entities/ProcessingJob.cs) before the exception that triggered
    // it is rethrown. If MassTransit has retries left, the next delivery
    // calls TryMarkJobRunningAsync again, which moves the job back to
    // Running — this per-attempt Failed state is only ever the in-between
    // snapshot, never the final word (VideoProcessingFaultConsumer owns the
    // terminal DeadLettered state once retries are exhausted).
    private async Task MarkJobAttemptFailedAsync(Guid videoId, string errorMessage, CancellationToken cancellationToken)
    {
        var job = await db.ProcessingJobs
            .Where(j => j.VideoId == videoId)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        job?.MarkFailed(errorMessage, clock.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
    }

    // Keeps the storage key's extension only when it is short and purely
    // alphanumeric (".mp4"); anything unusual yields no extension at all —
    // FFmpeg detects the real format from the file's content anyway.
    private static string GetSafeExtension(string objectKey)
    {
        var extension = Path.GetExtension(objectKey);

        return extension.Length is > 1 and <= 11 && extension.Skip(1).All(char.IsAsciiLetterOrDigit)
            ? extension
            : string.Empty;
    }

    private static async Task UploadFileAsync(
        IStorageService storage, string localPath, string objectKey, string contentType, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(localPath);
        await storage.UploadAsync(objectKey, stream, contentType, cancellationToken);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only — a leftover temp folder is a
            // disk-space concern, never a correctness one, so it must never fail the job.
        }
    }
}
