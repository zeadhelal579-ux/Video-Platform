// File: GetVideoHandlerTests.cs
namespace VideoPlatform.Tests.Application;

using VideoPlatform.Application.Videos.Queries.GetVideo;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Tests.TestSupport;
using Xunit;

public class GetVideoHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_returns_no_playlist_url_while_still_processing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var video = Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var handler = new GetVideoHandler(db, new FakeStorageService());

        var result = await handler.Handle(new GetVideoQuery(video.Id, ownerId));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.MasterPlaylistUrl);
        Assert.Equal("Queued", result.Value.Status);
    }

    [Fact]
    public async Task Handle_returns_a_playlist_url_once_the_video_is_ready()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var video = Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        video.MarkProcessing();
        video.MarkReady();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var handler = new GetVideoHandler(db, new FakeStorageService());

        var result = await handler.Handle(new GetVideoQuery(video.Id, ownerId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.MasterPlaylistUrl);
        Assert.Contains($"videos/{video.Id}/hls/master.m3u8", result.Value.MasterPlaylistUrl);
    }

    [Fact]
    public async Task Handle_denies_a_requester_who_is_not_the_owner()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var handler = new GetVideoHandler(db, new FakeStorageService());

        var result = await handler.Handle(new GetVideoQuery(video.Id, Guid.NewGuid()));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_returns_failure_for_an_unknown_video()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new GetVideoHandler(db, new FakeStorageService());

        var result = await handler.Handle(new GetVideoQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_returns_a_generic_message_for_a_failed_video_and_never_the_raw_job_error()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var video = Video.Create(Guid.NewGuid(), "My video", ownerId, "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        video.MarkProcessing();
        video.MarkFailed();
        db.Videos.Add(video);

        var job = ProcessingJob.Create(Guid.NewGuid(), video.Id);
        job.MarkRunning(Now);
        job.MarkDeadLettered("ffmpeg: cannot open /tmp/secret-path/source.mp4 via http://minio:9000", Now.AddMinutes(1));
        db.ProcessingJobs.Add(job);
        await db.SaveChangesAsync();

        var handler = new GetVideoHandler(db, new FakeStorageService());

        var result = await handler.Handle(new GetVideoQuery(video.Id, ownerId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.ErrorMessage));
        Assert.DoesNotContain("secret-path", result.Value.ErrorMessage);
        Assert.DoesNotContain("minio", result.Value.ErrorMessage);
    }
}
