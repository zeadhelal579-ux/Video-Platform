// File: ProcessingJob.cs
// Purpose: One row per processing *attempt*, not per video — this is what
// makes retries possible: a video can have three ProcessingJob rows (two
// failed, one succeeded) without losing the history of what went wrong
// (see ARCHITECTURE.md, "Why ProcessingJobs is separate from Video.Status").
// Logic/Approach: Status here tracks the attempt's own life cycle,
// completely independent from the owning Video's Status.
namespace VideoPlatform.Domain.Entities;

using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Domain.Exceptions;

public sealed class ProcessingJob : Entity
{
    // Upper bound for ErrorMessage. The EF configuration uses this same
    // constant for the column length, so the two can never drift apart.
    public const int MaxErrorMessageLength = 2000;

    public Guid VideoId { get; private set; }
    public JobStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private ProcessingJob()
    {
    } // EF Core

    private ProcessingJob(Guid id, Guid videoId) : base(id)
    {
        VideoId = videoId;
        Status = JobStatus.Pending;
        Attempts = 0;
    }

    // Called by ConfirmUploadHandler, in the same unit of work that moves the Video to Queued.
    public static ProcessingJob Create(Guid id, Guid videoId) => new(id, videoId);

    // Called by VideoProcessingConsumer the moment it picks the message off the queue.
    public void MarkRunning(DateTimeOffset now)
    {
        Status = JobStatus.Running;
        Attempts++;
        StartedAt ??= now;

        // A new attempt has started, so the previous attempt's end time no longer applies.
        CompletedAt = null;
    }

    public void MarkSucceeded(DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
            throw new InvalidStatusTransitionException(nameof(ProcessingJob), Status.ToString(), JobStatus.Succeeded.ToString());

        Status = JobStatus.Succeeded;
        CompletedAt = now;
    }

    // Called when this specific attempt fails but retries still remain —
    // MassTransit's retry middleware requeues the message automatically;
    // this method only records what happened to *this* attempt.
    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required.", nameof(errorMessage));

        Status = JobStatus.Failed;
        ErrorMessage = Truncate(errorMessage);
        CompletedAt = now;
    }

    // Called once retries are exhausted and RabbitMQ has routed the message
    // to its Dead-Letter Exchange — the terminal failure state for this job.
    public void MarkDeadLettered(string errorMessage, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required.", nameof(errorMessage));

        Status = JobStatus.DeadLettered;
        ErrorMessage = Truncate(errorMessage);
        CompletedAt = now;
    }

    // FFmpeg failures can carry the tool's whole stderr output. Storing that
    // untrimmed would overflow the column and make the very save that records
    // the failure throw, hiding the original error.
    private static string Truncate(string message) =>
        message.Length <= MaxErrorMessageLength ? message : message[..MaxErrorMessageLength];
}
