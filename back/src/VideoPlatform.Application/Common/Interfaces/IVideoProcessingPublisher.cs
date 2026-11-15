// File: IVideoProcessingPublisher.cs
// Purpose: The Application layer knows a "please process this video"
// message needs to go out once an upload is confirmed — it does not know
// RabbitMQ exists. RabbitMqPublisher (Infrastructure) is the only real
// implementation (ARCHITECTURE.md section 2.3).
namespace VideoPlatform.Application.Common.Interfaces;

using VideoPlatform.Application.Common.Contracts;

public interface IVideoProcessingPublisher
{
    Task PublishAsync(VideoProcessingRequested message, CancellationToken cancellationToken = default);
}
