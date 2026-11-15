// File: Program.cs
// Purpose: A .NET Generic Host (not a web host — the Worker exposes no
// HTTP endpoints). Registers the same AddInfrastructure(), plus
// MassTransit's two consumers and the transcoding service
// (ARCHITECTURE.md section 2.5).
// Logic/Approach: The Worker owns the receive endpoints. The transcode queue
// is limited to MaxConcurrentJobs messages in flight per instance, with a
// prefetch of the same size: an instance never holds more jobs than it can
// run, so a second Worker instance immediately picks up the rest — that is
// what makes "scale by adding Workers" actually work (ARCHITECTURE.md section 7).
using FFMpegCore;
using MassTransit;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Application.Videos.Commands.MarkVideoFailed;
using VideoPlatform.Application.Videos.Commands.MarkVideoReady;
using VideoPlatform.Domain.Common;
using VideoPlatform.Infrastructure;
using VideoPlatform.Infrastructure.Messaging;
using VideoPlatform.Worker.Consumers;
using VideoPlatform.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

var workerSection = builder.Configuration.GetSection(WorkerOptions.SectionName);
builder.Services.Configure<WorkerOptions>(workerSection);
var maxConcurrentJobs = Math.Max(1, (workerSection.Get<WorkerOptions>() ?? new WorkerOptions()).MaxConcurrentJobs);

// Optional: folder that holds the ffmpeg/ffprobe binaries when they are not
// on the PATH (typical for a Windows development machine). The Docker image
// installs both system-wide, so nothing needs configuring there.
var ffmpegFolder = builder.Configuration["FFmpeg:BinaryFolder"];
if (!string.IsNullOrWhiteSpace(ffmpegFolder))
    GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegFolder);

// The Worker is the only process that registers MassTransit consumers —
// the Api only ever sends messages (see Api/Program.cs).
builder.Services.AddInfrastructure(
    builder.Configuration,
    registerConsumers: bus =>
    {
        bus.AddConsumer<VideoProcessingConsumer>();
        bus.AddConsumer<VideoProcessingFaultConsumer>();
    },
    configureReceiveEndpoints: (cfg, context) =>
    {
        cfg.ReceiveEndpoint(MessagingEndpoints.VideoProcessingQueue, endpoint =>
        {
            endpoint.PrefetchCount = (ushort)maxConcurrentJobs;
            endpoint.ConcurrentMessageLimit = maxConcurrentJobs;
            endpoint.ConfigureConsumer<VideoProcessingConsumer>(context);
        });

        cfg.ReceiveEndpoint(MessagingEndpoints.VideoProcessingFaultQueue, endpoint =>
        {
            endpoint.ConfigureConsumer<VideoProcessingFaultConsumer>(context);
        });
    });

builder.Services.AddScoped<ITranscodingService, FFmpegTranscodingService>();

// The two Application handlers the Worker calls in-process
// (ARCHITECTURE.md section 2.5) — same explicit-registration style as
// Api/Program.cs, against the closed ICommandHandler<,> interface.
builder.Services.AddScoped<ICommandHandler<MarkVideoReadyCommand, Result>, MarkVideoReadyHandler>();
builder.Services.AddScoped<ICommandHandler<MarkVideoFailedCommand, Result>, MarkVideoFailedHandler>();

var host = builder.Build();
host.Run();

// Exposed so VideoPlatform.Tests can reference this entry point if needed.
public partial class Program
{
}
