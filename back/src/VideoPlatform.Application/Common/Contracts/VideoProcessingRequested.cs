// File: VideoProcessingRequested.cs
// Purpose: The message published once a video upload is confirmed, and
// consumed by the Worker to start transcoding. A plain, framework-free
// record — this guarantees the message producer (Api) and consumer
// (Worker) never drift out of sync, since both reference this exact type.
// Note on placement: ARCHITECTURE.md originally lists this file under
// Infrastructure/Messaging/Contracts/. It lives here instead because
// IVideoProcessingPublisher (next to this file) has to reference it, and
// Application is not allowed to depend on Infrastructure — dependencies
// only point inward (ARCHITECTURE.md section 2, first paragraph). Both
// RabbitMqPublisher and VideoProcessingConsumer (Infrastructure/Worker)
// already reference Application, so they still see this exact same type —
// the "one shared contract" guarantee the original doc describes is fully
// preserved, only the folder changed.
namespace VideoPlatform.Application.Common.Contracts;

public sealed record VideoProcessingRequested(Guid VideoId, string OriginalStoragePath);
