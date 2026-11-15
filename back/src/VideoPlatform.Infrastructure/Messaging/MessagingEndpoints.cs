// File: MessagingEndpoints.cs
// Purpose: The queue names shared by the process that sends work (Api) and
// the process that receives it (Worker). Defined once so the two sides can
// never disagree about where a "process this video" message goes.
namespace VideoPlatform.Infrastructure.Messaging;

public static class MessagingEndpoints
{
    // Where VideoProcessingRequested messages are delivered to the Worker.
    public const string VideoProcessingQueue = "video-processing";

    // Where the Worker's Fault<VideoProcessingRequested> consumer listens
    // (the notification MassTransit publishes once every retry is used up).
    public const string VideoProcessingFaultQueue = "video-processing-faults";
}
