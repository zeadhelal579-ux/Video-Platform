// File: RequestUploadUrlHandlerTests.cs
namespace VideoPlatform.Tests.Application;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Videos.Commands.RequestUploadUrl;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Tests.TestSupport;
using Xunit;

public class RequestUploadUrlHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_creates_a_pending_video_and_returns_a_signed_url()
    {
        await using var db = TestAppDbContextFactory.Create();
        var storage = new FakeStorageService();
        var handler = new RequestUploadUrlHandler(db, storage, new FakeDateTimeProvider(Now));
        var ownerId = Guid.NewGuid();

        var result = await handler.Handle(new RequestUploadUrlCommand(ownerId, "My video", "movie.mp4"));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.VideoId);
        Assert.Contains("signed=true", result.Value.UploadUrl);

        var savedVideo = await db.Videos.FindAsync(result.Value.VideoId);
        Assert.NotNull(savedVideo);
        Assert.Equal(VideoStatus.Pending, savedVideo!.Status);
        Assert.Equal(ownerId, savedVideo.OwnerId);
    }

    [Fact]
    public async Task Handle_rejects_a_blank_file_name()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new RequestUploadUrlHandler(db, new FakeStorageService(), new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new RequestUploadUrlCommand(Guid.NewGuid(), "Title", ""));

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_rejects_a_blank_title(string title)
    {
        await using var db = TestAppDbContextFactory.Create();
        var storage = new FakeStorageService();
        var handler = new RequestUploadUrlHandler(db, storage, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new RequestUploadUrlCommand(Guid.NewGuid(), title, "movie.mp4"));

        Assert.True(result.IsFailure);
        Assert.Empty(storage.RequestedUploadUrlKeys);
        Assert.Empty(db.Videos);
    }

    [Fact]
    public async Task Handle_rejects_a_title_longer_than_the_column_limit()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new RequestUploadUrlHandler(db, new FakeStorageService(), new FakeDateTimeProvider(Now));
        var tooLong = new string('a', RequestUploadUrlHandler.MaxTitleLength + 1);

        var result = await handler.Handle(new RequestUploadUrlCommand(Guid.NewGuid(), tooLong, "movie.mp4"));

        Assert.True(result.IsFailure);
        Assert.Empty(db.Videos);
    }

    [Theory]
    [InlineData("movie.mp4", "movie.mp4")]
    [InlineData("my holiday video.MP4", "my_holiday_video.mp4")]
    [InlineData("a/b/c/final cut (v2).mov", "final_cut_v2.mov")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\evil.mp4", "evil.mp4")]
    [InlineData("\u0641\u064a\u062f\u064a\u0648.mp4", "video.mp4")]
    [InlineData("clip", "clip")]
    public async Task Handle_builds_a_safe_object_key_from_an_untrusted_file_name(string fileName, string expectedName)
    {
        await using var db = TestAppDbContextFactory.Create();
        var storage = new FakeStorageService();
        var handler = new RequestUploadUrlHandler(db, storage, new FakeDateTimeProvider(Now));

        var result = await handler.Handle(new RequestUploadUrlCommand(Guid.NewGuid(), "Title", fileName));

        Assert.True(result.IsSuccess);
        var expectedKey = $"videos/{result.Value.VideoId}/original/{expectedName}";
        Assert.Equal(expectedKey, Assert.Single(storage.RequestedUploadUrlKeys));

        var savedVideo = await db.Videos.FindAsync(result.Value.VideoId);
        Assert.Equal(expectedKey, savedVideo!.OriginalStoragePath);
    }
}
