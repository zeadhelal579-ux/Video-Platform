// File: VideoStatus.cs
// Purpose: The full lifecycle of a Video, referenced everywhere a video's
// state matters (Domain, Application, Api, Worker, frontend). Defined once
// here so every layer agrees on the same five states in the same order
// (see FLOW.md and ARCHITECTURE.md section 2.1).
namespace VideoPlatform.Domain.Enums;

public enum VideoStatus
{
    // Upload URL issued, but the client has not confirmed the raw file
    // finished uploading yet.
    Pending = 0,

    // Client confirmed the upload; a VideoProcessingRequested message has
    // been published to RabbitMQ.
    Queued = 1,

    // A Worker instance has picked up the job and is actively transcoding.
    Processing = 2,

    // All renditions were produced and uploaded successfully; the video can be streamed.
    Ready = 3,

    // Processing exhausted its retries; see the related ProcessingJob.ErrorMessage.
    Failed = 4
}
