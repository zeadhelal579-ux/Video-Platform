// File: RabbitMqPublisher.cs
// Purpose: Implements IVideoProcessingPublisher using MassTransit —
// removes the need to hand-write RabbitMQ connection, retry, and
// serialization logic (TOOLS.md, "MassTransit").
// Logic/Approach: "Process this video" is a command for exactly one
// consumer, so it is SENT to the Worker's queue rather than published to a
// topic. Sending declares the queue on demand, which means a job is never
// lost because the Worker has not started (or its queue not been created)
// yet — a plain publish would be dropped when no queue is bound.
namespace VideoPlatform.Infrastructure.Messaging;

using MassTransit;
using VideoPlatform.Application.Common.Contracts;
using VideoPlatform.Application.Common.Interfaces;

public sealed class RabbitMqPublisher(ISendEndpointProvider sendEndpointProvider) : IVideoProcessingPublisher
{
    private static readonly Uri VideoProcessingAddress = new($"queue:{MessagingEndpoints.VideoProcessingQueue}");

    // Delivers the message to the video-processing queue (waits for the broker's confirmation).
    public async Task PublishAsync(VideoProcessingRequested message, CancellationToken cancellationToken = default)
    {
        var endpoint = await sendEndpointProvider.GetSendEndpoint(VideoProcessingAddress);
        await endpoint.Send(message, cancellationToken);
    }
}
