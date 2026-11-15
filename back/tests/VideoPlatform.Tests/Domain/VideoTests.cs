// File: VideoTests.cs
// Purpose: Verifies Video's status-transition rules — the exact invariant
// ARCHITECTURE.md section 2.1 describes: "the only place a video's status
// is allowed to change... stops a bug elsewhere from putting a video into
// an invalid state."
namespace VideoPlatform.Tests.Domain;

using VideoPlatform.Domain.Entities;
using VideoPlatform.Domain.Enums;
using VideoPlatform.Domain.Exceptions;
using Xunit;

public class VideoTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_at_pending_status()
    {
        var video = CreateVideo();

        Assert.Equal(VideoStatus.Pending, video.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_title(string blankTitle)
    {
        Assert.Throws<ArgumentException>(() =>
            Video.Create(Guid.NewGuid(), blankTitle, Guid.NewGuid(), "videos/1/original/file.mp4", Now));
    }

    [Fact]
    public void MarkQueued_from_pending_succeeds()
    {
        var video = CreateVideo();

        video.MarkQueued();

        Assert.Equal(VideoStatus.Queued, video.Status);
    }

    [Fact]
    public void MarkQueued_called_twice_throws_invalid_transition()
    {
        var video = CreateVideo();
        video.MarkQueued();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkQueued());
    }

    [Fact]
    public void Full_happy_path_from_pending_to_ready_succeeds()
    {
        var video = CreateVideo();

        video.MarkQueued();
        video.MarkProcessing();
        video.MarkReady();

        Assert.Equal(VideoStatus.Ready, video.Status);
    }

    [Fact]
    public void MarkProcessing_from_pending_throws_invalid_transition()
    {
        var video = CreateVideo();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkProcessing());
    }

    [Fact]
    public void MarkProcessing_called_again_while_already_processing_does_not_throw()
    {
        // Regression test for the retry bug: MassTransit redelivers the same
        // message on a transient failure, and VideoProcessingConsumer calls
        // MarkProcessing() again on every redelivery before it retries the
        // real work. The original guard only allowed Queued -> Processing,
        // so a second delivery threw InvalidStatusTransitionException
        // instead of actually retrying the download/transcode/upload — this
        // pins down the corrected, idempotent behavior.
        var video = CreateVideo();
        video.MarkQueued();
        video.MarkProcessing();

        video.MarkProcessing();

        Assert.Equal(VideoStatus.Processing, video.Status);
    }

    [Fact]
    public void MarkProcessing_from_ready_throws_invalid_transition()
    {
        var video = CreateVideo();
        video.MarkQueued();
        video.MarkProcessing();
        video.MarkReady();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkProcessing());
    }

    [Fact]
    public void MarkProcessing_from_failed_throws_invalid_transition()
    {
        var video = CreateVideo();
        video.MarkQueued();
        video.MarkFailed();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkProcessing());
    }

    [Fact]
    public void MarkReady_from_pending_throws_invalid_transition()
    {
        var video = CreateVideo();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkReady());
    }

    [Fact]
    public void MarkFailed_from_queued_succeeds()
    {
        var video = CreateVideo();
        video.MarkQueued();

        video.MarkFailed();

        Assert.Equal(VideoStatus.Failed, video.Status);
    }

    [Fact]
    public void MarkFailed_from_processing_succeeds()
    {
        var video = CreateVideo();
        video.MarkQueued();
        video.MarkProcessing();

        video.MarkFailed();

        Assert.Equal(VideoStatus.Failed, video.Status);
    }

    [Fact]
    public void MarkFailed_from_pending_throws_invalid_transition()
    {
        var video = CreateVideo();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkFailed());
    }

    [Fact]
    public void MarkFailed_from_ready_throws_invalid_transition()
    {
        var video = CreateVideo();
        video.MarkQueued();
        video.MarkProcessing();
        video.MarkReady();

        Assert.Throws<InvalidStatusTransitionException>(() => video.MarkFailed());
    }

    private static Video CreateVideo() =>
        Video.Create(Guid.NewGuid(), "My video", Guid.NewGuid(), "videos/1/original/file.mp4", Now);
}
