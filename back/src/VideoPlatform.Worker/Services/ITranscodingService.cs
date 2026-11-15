// File: ITranscodingService.cs
// Purpose: Abstracts "turn this local file into HLS renditions" so
// VideoProcessingConsumer can be unit-tested with a fake instead of
// shelling out to the real FFmpeg binary.
namespace VideoPlatform.Worker.Services;

using VideoPlatform.Domain.Enums;

public interface ITranscodingService
{
    Task<TranscodingResult> TranscodeAsync(string sourceFilePath, string outputDirectory, CancellationToken cancellationToken = default);
}

// One produced quality: the local variant playlist, its (video) bitrate in
// kbps, and every .ts segment file FFmpeg wrote for it.
public sealed record TranscodingOutput(Resolution Resolution, string LocalM3u8Path, int Bitrate, IReadOnlyList<string> SegmentPaths);

// The full output of one transcode: the local master playlist plus every
// rendition that was produced (1 to 3, depending on the source's size).
public sealed record TranscodingResult(string MasterPlaylistLocalPath, IReadOnlyList<TranscodingOutput> Renditions);
