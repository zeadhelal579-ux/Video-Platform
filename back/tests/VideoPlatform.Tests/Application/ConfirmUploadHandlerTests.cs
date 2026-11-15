// File: ConfirmUploadHandlerTests.cs
namespace VideoPlatform.Tests.Application;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Videos.Commands.ConfirmUpload;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Tests.TestSupport;
using Xunit;

public class ConfirmUploadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_moves_video_to_queued_creates_a_job_and_publishes_one_message()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var video = Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/file.mp4", Now);
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var publisher = new FakeVideoProcessingPublisher();
        var handler = new ConfirmUploadHandler(db, publisher, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new ConfirmUploadCommand(video.Id, ownerId));

        Assert.True(result.IsSuccess);
        Assert.Equal(VideoStatus.Queued, video.Status);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal(video.Id, publisher.PublishedMessages[0].VideoId);

        var job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.VideoId == video.Id);
        Assert.NotNull(job);
    }

    [Fact]
    public async Task Handle_rejects_a_requester_who_is_not_the_owner()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var handler = new ConfirmUploadHandler(db, new FakeVideoProcessingPublisher(), new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new ConfirmUploadCommand(video.Id, Guid.NewGuid()));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_returns_failure_for_an_unknown_video()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new ConfirmUploadHandler(db, new FakeVideoProcessingPublisher(), new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new ConfirmUploadCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_marks_the_video_failed_instead_of_leaving_it_stuck_at_queued_when_publishing_throws()
    {
        // Regression test: previously, a broker failure here (after
        // SaveChangesAsync already committed Status = Queued) left the
        // video stuck forever — no message ever published, and no way to
        // retry, since MarkQueued() only accepts Pending -> Queued. See
        // ConfirmUploadHandler.cs.
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var video = Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/file.mp4", Now);
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var publisher = new FakeVideoProcessingPublisher { ThrowOnPublish = true };
        var handler = new ConfirmUploadHandler(db, publisher, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new ConfirmUploadCommand(video.Id, ownerId));

        Assert.True(result.IsFailure);
        Assert.Equal(VideoStatus.Failed, video.Status);

        var job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.VideoId == video.Id);
        Assert.NotNull(job);
        Assert.Equal(JobStatus.Failed, job!.Status);
        Assert.False(string.IsNullOrEmpty(job.ErrorMessage));
    }

    [Fact]
    public async Task Handle_rejects_confirming_the_same_upload_twice()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var video = Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/file.mp4", Now);
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var publisher = new FakeVideoProcessingPublisher();
        var handler = new ConfirmUploadHandler(db, publisher, new FakeDateTimeProvider(Now));

        var first = await handler.Handle(new ConfirmUploadCommand(video.Id, ownerId));
        var second = await handler.Handle(new ConfirmUploadCommand(video.Id, ownerId));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal(1, await db.ProcessingJobs.CountAsync(j => j.VideoId == video.Id));
    }
}
