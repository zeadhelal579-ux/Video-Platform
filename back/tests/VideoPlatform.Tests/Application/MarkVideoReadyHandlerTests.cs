// File: MarkVideoReadyHandlerTests.cs
namespace VideoPlatform.Tests.Application;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Videos.Commands.MarkVideoReady;
using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Tests.TestSupport;
using Xunit;

public class MarkVideoReadyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_writes_one_rendition_per_input_and_marks_the_video_ready()
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

        var handler = new MarkVideoReadyHandler(db, new FakeDateTimeProvider(Now.AddMinutes(2)));

        var command = new MarkVideoReadyCommand(video.Id, new[]
        {
            new RenditionInput(Resolution.R1080p, "videos/x/hls/1080p/playlist.m3u8", 5000),
            new RenditionInput(Resolution.R720p, "videos/x/hls/720p/playlist.m3u8", 2800),
            new RenditionInput(Resolution.R480p, "videos/x/hls/480p/playlist.m3u8", 1400)
        });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(VideoStatus.Ready, video.Status);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal(3, await db.VideoRenditions.CountAsync(r => r.VideoId == video.Id));
    }

    [Fact]
    public async Task Handle_rejects_an_empty_rendition_list()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        video.MarkProcessing();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var handler = new MarkVideoReadyHandler(db, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new MarkVideoReadyCommand(video.Id, Array.Empty<RenditionInput>()));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_returns_failure_for_an_unknown_video()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new MarkVideoReadyHandler(db, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new MarkVideoReadyCommand(Guid.NewGuid(), new[]
        {
            new RenditionInput(Resolution.R1080p, "videos/x/hls/1080p/playlist.m3u8", 5000)
        }));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_rejects_a_video_that_is_not_processing_and_writes_nothing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var video = Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
        video.MarkQueued();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        var handler = new MarkVideoReadyHandler(db, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new MarkVideoReadyCommand(video.Id, new[]
        {
            new RenditionInput(Resolution.R480p, "videos/x/hls/480p/playlist.m3u8", 1400)
        }));

        Assert.True(result.IsFailure);
        Assert.Equal(VideoStatus.Queued, video.Status);
        Assert.Equal(0, await db.VideoRenditions.CountAsync());
    }
}
