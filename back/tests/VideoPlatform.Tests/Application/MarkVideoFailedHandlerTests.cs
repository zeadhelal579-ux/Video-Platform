// File: MarkVideoFailedHandlerTests.cs
namespace VideoPlatform.Tests.Application;

using VideoPlatform.Application.Videos.Commands.MarkVideoFailed;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Tests.TestSupport;
using Xunit;

public class MarkVideoFailedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_marks_the_video_failed_and_the_job_dead_lettered()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        video.MarkProcessing();
        db.Videos.Add(video);

        var job = ProcessingJob.Create(Guid.NewGuid(), video.Id);
        job.MarkRunning(Now);
        db.ProcessingJobs.Add(job);
        await db.SaveChangesAsync();

        var handler = new MarkVideoFailedHandler(db, new FakeDateTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(new MarkVideoFailedCommand(video.Id, "ffmpeg exited with code 1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(VideoStatus.Failed, video.Status);
        Assert.Equal(JobStatus.DeadLettered, job.Status);
        Assert.Equal("ffmpeg exited with code 1", job.ErrorMessage);
    }

    [Fact]
    public async Task Handle_returns_failure_for_an_unknown_video()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new MarkVideoFailedHandler(db, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new MarkVideoFailedCommand(Guid.NewGuid(), "some error"));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_leaves_a_video_that_is_already_ready_untouched()
    {
        // A late or duplicated fault notification must never undo a finished video.
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        video.MarkProcessing();
        video.MarkReady();
        db.Videos.Add(video);

        var job = ProcessingJob.Create(Guid.NewGuid(), video.Id);
        job.MarkRunning(Now);
        job.MarkSucceeded(Now.AddMinutes(1));
        db.ProcessingJobs.Add(job);
        await db.SaveChangesAsync();

        var handler = new MarkVideoFailedHandler(db, new FakeDateTimeProvider(Now.AddMinutes(2)));

        var result = await handler.Handle(new MarkVideoFailedCommand(video.Id, "late fault"));

        Assert.True(result.IsFailure);
        Assert.Equal(VideoStatus.Ready, video.Status);
        Assert.Equal(JobStatus.Succeeded, job.Status);
    }

    [Fact]
    public async Task Handle_truncates_a_very_long_error_message_instead_of_failing_to_save()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        video.MarkProcessing();
        db.Videos.Add(video);

        var job = ProcessingJob.Create(Guid.NewGuid(), video.Id);
        job.MarkRunning(Now);
        db.ProcessingJobs.Add(job);
        await db.SaveChangesAsync();

        var handler = new MarkVideoFailedHandler(db, new FakeDateTimeProvider(Now.AddMinutes(1)));

        var result = await handler.Handle(new MarkVideoFailedCommand(video.Id, new string('z', 10_000)));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProcessingJob.MaxErrorMessageLength, job.ErrorMessage!.Length);
    }
}
