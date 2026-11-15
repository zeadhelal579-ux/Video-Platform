// File: WorkerOptions.cs
// Purpose: Binds the "Worker" section of appsettings.json / environment
// variables (Worker__MaxConcurrentJobs, Worker__TranscodeTimeoutMinutes).
namespace VideoPlatform.Worker.Services;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    // How many videos ONE Worker instance transcodes at the same time. FFmpeg
    // already spreads a single video across every CPU core, so 1 is the right
    // default; to process more videos in parallel, run more Worker instances.
    public int MaxConcurrentJobs { get; set; } = 1;

    // Hard stop for one processing attempt (download + transcode + upload), so
    // a hung FFmpeg process can never block the queue forever. A timed-out
    // attempt counts as a failure and is retried like any other.
    public int TranscodeTimeoutMinutes { get; set; } = 180;
}
