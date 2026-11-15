// File: Video.cs
// Purpose: The central entity of the platform. Holds Title, OwnerId,
// Status, OriginalStoragePath (see ARCHITECTURE.md section 2.1).
// Logic/Approach: Every Mark*() method below is the *only* legal way to
// change Status. Each one checks the current status before moving to the
// next one, so a bug anywhere else in the codebase can never push a video
// into an invalid state (e.g. Ready with zero renditions, or Processing
// jumped to directly from Pending).
namespace VideoPlatform.Domain.Entities;

using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Domain.Exceptions;

public sealed class Video : Entity
{
    public string Title { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public VideoStatus Status { get; private set; }
    public string OriginalStoragePath { get; private set; } = null!;
    public string? ThumbnailPath { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Video()
    {
    } // EF Core

    private Video(Guid id, string title, Guid ownerId, string originalStoragePath, DateTimeOffset now)
        : base(id)
    {
        Title = title;
        OwnerId = ownerId;
        OriginalStoragePath = originalStoragePath;
        Status = VideoStatus.Pending;
        CreatedAt = now;
    }

    // Called by RequestUploadUrlHandler right after a pre-signed upload URL
    // is issued. Starts at Pending: the URL exists, nothing is uploaded yet.
    public static Video Create(Guid id, string title, Guid ownerId, string originalStoragePath, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(originalStoragePath))
            throw new ArgumentException("Original storage path is required.", nameof(originalStoragePath));

        return new Video(id, title.Trim(), ownerId, originalStoragePath, now);
    }

    // Called by ConfirmUploadHandler once the client confirms the raw file finished uploading.
    public void MarkQueued()
    {
        EnsureTransitionAllowed(VideoStatus.Pending, VideoStatus.Queued);
        Status = VideoStatus.Queued;
    }

    // Called by VideoProcessingConsumer the instant a Worker picks the job up
    // — and again on every MassTransit redelivery of the same message after
    // a transient failure (Infrastructure/DependencyInjection.cs,
    // cfg.UseMessageRetry). That means the legal predecessor is not only
    // Queued: a retry is delivered while Status is already Processing from
    // the first attempt, and that must succeed too (idempotent), or every
    // retry would die on this guard before FFmpeg ever runs again.
    public void MarkProcessing()
    {
        if (Status is not (VideoStatus.Queued or VideoStatus.Processing))
            throw new InvalidStatusTransitionException(nameof(Video), Status.ToString(), VideoStatus.Processing.ToString());

        Status = VideoStatus.Processing;
    }

    // Called by MarkVideoReadyHandler once every rendition has been uploaded successfully.
    public void MarkReady()
    {
        EnsureTransitionAllowed(VideoStatus.Processing, VideoStatus.Ready);
        Status = VideoStatus.Ready;
    }

    // Called by MarkVideoFailedHandler once retries are exhausted and the job is dead-lettered.
    public void MarkFailed()
    {
        // Unlike the transitions above, failure has two legal predecessors:
        // Queued (the job never even started, e.g. the Worker crashed on
        // startup) or Processing (it started but the transcode itself failed).
        if (Status is not (VideoStatus.Queued or VideoStatus.Processing))
            throw new InvalidStatusTransitionException(nameof(Video), Status.ToString(), VideoStatus.Failed.ToString());

        Status = VideoStatus.Failed;
    }

    public void SetThumbnail(string thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath))
            throw new ArgumentException("Thumbnail path is required.", nameof(thumbnailPath));

        ThumbnailPath = thumbnailPath;
    }

    // Centralizes the "is this transition legal" check so every Mark*()
    // method above stays a one-liner and the rule lives in exactly one place.
    private void EnsureTransitionAllowed(VideoStatus expectedCurrent, VideoStatus target)
    {
        if (Status != expectedCurrent)
            throw new InvalidStatusTransitionException(nameof(Video), Status.ToString(), target.ToString());
    }
}
