// File: ProcessingJobTests.cs
// Purpose: Verifies ProcessingJob's own life cycle — Attempts incrementing
// on every run, and the terminal states carrying their error message
// (ARCHITECTURE.md section 2.1).
namespace VideoPlatform.Tests.Domain;

using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Domain.Exceptions;
using Xunit;

public class ProcessingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_pending_with_zero_attempts()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(0, job.Attempts);
    }

    [Fact]
    public void MarkRunning_increments_attempts_on_every_call()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());

        job.MarkRunning(Now);
        job.MarkRunning(Now.AddMinutes(5));

        Assert.Equal(2, job.Attempts);
    }

    [Fact]
    public void MarkSucceeded_without_running_first_throws_invalid_transition()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<InvalidStatusTransitionException>(() => job.MarkSucceeded(Now));
    }

    [Fact]
    public void MarkSucceeded_after_running_sets_completed_at()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());
        job.MarkRunning(Now);

        job.MarkSucceeded(Now.AddMinutes(3));

        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal(Now.AddMinutes(3), job.CompletedAt);
    }

    [Fact]
    public void MarkDeadLettered_sets_status_and_error_message()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());
        job.MarkRunning(Now);

        job.MarkDeadLettered("ffmpeg crashed", Now.AddMinutes(1));

        Assert.Equal(JobStatus.DeadLettered, job.Status);
        Assert.Equal("ffmpeg crashed", job.ErrorMessage);
    }

    [Fact]
    public void MarkRunning_clears_the_completion_time_left_by_the_previous_attempt()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());
        job.MarkRunning(Now);
        job.MarkFailed("first attempt failed", Now.AddMinutes(1));
        Assert.NotNull(job.CompletedAt);

        job.MarkRunning(Now.AddMinutes(2));

        Assert.Null(job.CompletedAt);
        Assert.Equal(JobStatus.Running, job.Status);
        Assert.Equal(2, job.Attempts);
    }

    [Fact]
    public void MarkFailed_truncates_overlong_error_messages_to_the_column_limit()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());
        job.MarkRunning(Now);

        job.MarkFailed(new string('x', ProcessingJob.MaxErrorMessageLength * 3), Now.AddMinutes(1));

        Assert.Equal(ProcessingJob.MaxErrorMessageLength, job.ErrorMessage!.Length);
    }

    [Fact]
    public void MarkDeadLettered_truncates_overlong_error_messages_to_the_column_limit()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid());
        job.MarkRunning(Now);

        job.MarkDeadLettered(new string('y', ProcessingJob.MaxErrorMessageLength + 1), Now.AddMinutes(1));

        Assert.Equal(ProcessingJob.MaxErrorMessageLength, job.ErrorMessage!.Length);
    }
}
