// File: VideoProcessingFaultConsumer.cs
// Purpose: Runs once MassTransit's retry middleware has exhausted every
// attempt for VideoProcessingRequested and the message has been parked in
// the "video-processing_error" queue (MassTransit's dead-letter queue, kept
// for manual inspection or replay) — the terminal failure path
// (FLOW.md, Video Processing Flow, step 7). The video is never silently
// lost: the error stays attached to the ProcessingJob row.
// Logic/Approach: Split out from VideoProcessingConsumer.cs into its own
// file because it is registered separately in Worker/Program.cs.
namespace VideoPlatform.Worker.Consumers;

using MassTransit;
using VideoPlatform.Application.Common.Contracts;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Application.Videos.Commands.MarkVideoFailed;
using VideoPlatform.Domain.Common;

public sealed class VideoProcessingFaultConsumer(
    ICommandHandler<MarkVideoFailedCommand, Result> markFailedHandler,
    ILogger<VideoProcessingFaultConsumer> logger) : IConsumer<Fault<VideoProcessingRequested>>
{
    public async Task Consume(ConsumeContext<Fault<VideoProcessingRequested>> context)
    {
        var videoId = context.Message.Message.VideoId;
        var errorMessage = context.Message.Exceptions.FirstOrDefault()?.Message ?? "Processing failed for an unknown reason.";

        logger.LogError("Video {VideoId} was dead-lettered: {Error}", videoId, errorMessage);

        var result = await markFailedHandler.Handle(
            new MarkVideoFailedCommand(videoId, errorMessage), context.CancellationToken);

        if (result.IsFailure)
            logger.LogWarning("MarkVideoFailed failed for video {VideoId}: {Error}", videoId, result.Error);
    }
}
