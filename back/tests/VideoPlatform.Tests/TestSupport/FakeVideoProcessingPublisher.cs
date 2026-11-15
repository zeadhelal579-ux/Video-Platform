// File: FakeVideoProcessingPublisher.cs
// Purpose: Records every message a handler tried to publish, instead of
// actually talking to RabbitMQ — lets ConfirmUploadHandlerTests assert
// exactly one message went out, with the right VideoId. ThrowOnPublish
// simulates a broker outage, for testing ConfirmUploadHandler's catch
// path around IVideoProcessingPublisher.PublishAsync.
namespace VideoPlatform.Tests.TestSupport;

using VideoPlatform.Application.Common.Contracts;
using VideoPlatform.Application.Common.Interfaces;

public sealed class FakeVideoProcessingPublisher : IVideoProcessingPublisher
{
    public List<VideoProcessingRequested> PublishedMessages { get; } = [];

    // When true, PublishAsync throws instead of recording the message —
    // simulates RabbitMQ being unreachable.
    public bool ThrowOnPublish { get; set; }

    public Task PublishAsync(VideoProcessingRequested message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
            throw new InvalidOperationException("Simulated broker outage.");

        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }
}
