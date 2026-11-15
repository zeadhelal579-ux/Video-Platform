// File: FFmpegTranscodingService.cs
// Purpose: Wraps FFMpegCore calls — takes a local file path, produces up to
// three renditions (1080p/720p/480p) plus a master playlist
// (ARCHITECTURE.md section 2.5; FLOW.md, Video Processing Flow, step 4).
// Logic/Approach: The HLS-specific flags are passed through
// .WithCustomArgument(...) (raw ffmpeg CLI flags) rather than a higher-level
// helper, since raw flags are stable across FFMpegCore versions even when its
// C# wrapper API is not. What the flags guarantee:
//  - the aspect ratio is preserved (scale=-2:H) — portrait and ultrawide
//    videos are not stretched to 16:9;
//  - qualities taller than the source are skipped (see ResolutionPreset);
//  - yuv420p + AAC stereo, the combination every browser can decode;
//  - keyframes on an exact 6-second grid in every rendition, so a player can
//    switch quality at any segment boundary without a visual glitch;
//  - a capped bitrate (-maxrate/-bufsize), so the BANDWIDTH advertised in the
//    master playlist is honest and ABR switching decisions are sound.
namespace VideoPlatform.Worker.Services;

using System.Text;
using FFMpegCore;
using VideoPlatform.Domain.Enums;

public sealed class FFmpegTranscodingService(ILogger<FFmpegTranscodingService> logger) : ITranscodingService
{
    // Segment length in seconds; keyframes are forced on exactly this grid.
    private const int SegmentSeconds = 6;

    // Audio is the same in every rendition: stereo AAC at this bitrate (kbps).
    private const int AudioBitrateKbps = 128;

    // Transcodes the source into every rendition that makes sense for its
    // size, one after another, then writes the master playlist.
    public async Task<TranscodingResult> TranscodeAsync(string sourceFilePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var (hasVideo, sourceHeight) = await ProbeAsync(sourceFilePath);
        if (!hasVideo)
            throw new InvalidOperationException("The uploaded file contains no video stream.");

        var renditions = new List<TranscodingOutput>();

        // One quality at a time, sequentially. A single Worker instance is
        // already one FFmpeg process per video; horizontal scaling happens
        // by running MORE Worker instances (ARCHITECTURE.md section 7),
        // not by parallelizing renditions inside one instance.
        foreach (var (resolution, preset) in ResolutionPreset.SelectFor(sourceHeight))
        {
            cancellationToken.ThrowIfCancellationRequested();
            renditions.Add(await TranscodeRenditionAsync(sourceFilePath, outputDirectory, resolution, preset, cancellationToken));
        }

        var masterPlaylistPath = Path.Combine(outputDirectory, "master.m3u8");
        await File.WriteAllTextAsync(masterPlaylistPath, BuildMasterPlaylist(renditions), cancellationToken);

        return new TranscodingResult(masterPlaylistPath, renditions);
    }

    // Asks ffprobe what the file contains. A probe failure is deliberately
    // not fatal: "unknown" lets FFmpeg itself be the judge (it fails with a
    // clear error if the file is unusable), and every rendition is produced.
    private async Task<(bool HasVideo, int? Height)> ProbeAsync(string sourceFilePath)
    {
        try
        {
            var analysis = await FFProbe.AnalyseAsync(sourceFilePath);
            var video = analysis.PrimaryVideoStream;

            return (video is not null, video?.Height);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not probe {SourceFile}; every rendition will be produced.", sourceFilePath);
            return (true, null);
        }
    }

    // Runs FFmpeg once: source -> one quality as an HLS playlist plus .ts segments.
    private static async Task<TranscodingOutput> TranscodeRenditionAsync(
        string sourceFilePath,
        string outputDirectory,
        Resolution resolution,
        ResolutionPreset.Preset preset,
        CancellationToken cancellationToken)
    {
        var renditionDir = Path.Combine(outputDirectory, ResolutionPreset.FolderNames[resolution]);
        Directory.CreateDirectory(renditionDir);

        var variantPlaylistPath = Path.Combine(renditionDir, "playlist.m3u8");
        var segmentPattern = Path.Combine(renditionDir, "segment_%05d.ts");

        // Rate control: aim for the preset's bitrate, allow short bursts of
        // 10% above it, and smooth over a two-second window.
        var maxRateKbps = (int)(preset.BitrateKbps * 1.1);
        var bufferKbps = preset.BitrateKbps * 2;

        bool completed;
        try
        {
            completed = await FFMpegArguments
                .FromFileInput(sourceFilePath)
                .OutputToFile(variantPlaylistPath, overwrite: true, options => options
                    .WithVideoCodec("libx264")
                    .WithAudioCodec("aac")
                    .WithCustomArgument($"-vf scale=-2:{preset.Height}")
                    .WithCustomArgument("-pix_fmt yuv420p")
                    .WithCustomArgument($"-b:v {preset.BitrateKbps}k -maxrate {maxRateKbps}k -bufsize {bufferKbps}k")
                    .WithCustomArgument($"-b:a {AudioBitrateKbps}k -ac 2")
                    .WithCustomArgument("-sc_threshold 0")
                    .WithCustomArgument($"-force_key_frames \"expr:gte(t,n_forced*{SegmentSeconds})\"")
                    .WithCustomArgument($"-hls_time {SegmentSeconds}")
                    .WithCustomArgument("-hls_playlist_type vod")
                    .WithCustomArgument("-hls_flags independent_segments")
                    .WithCustomArgument($"-hls_segment_filename \"{segmentPattern}\""))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // FFMpegCore reports a cancelled run in its own way (an FFMpegException
            // after killing the process); surface it as the standard cancellation instead.
            throw new OperationCanceledException(cancellationToken);
        }

        if (!completed)
            throw new InvalidOperationException($"FFmpeg did not complete the {resolution} rendition.");

        // Never upload a half-written result as if it were a finished rendition.
        var segmentPaths = Directory.GetFiles(renditionDir, "segment_*.ts").OrderBy(path => path, StringComparer.Ordinal).ToList();
        if (!File.Exists(variantPlaylistPath) || segmentPaths.Count == 0)
            throw new InvalidOperationException($"FFmpeg produced no output for the {resolution} rendition.");

        return new TranscodingOutput(resolution, variantPlaylistPath, preset.BitrateKbps, segmentPaths);
    }

    // Builds the master .m3u8: lists every variant playlist with its
    // bandwidth — this is what lets Video.js perform ABR switching between
    // them (README.md, "Adaptive Bitrate Streaming"). The optional
    // RESOLUTION attribute is left out on purpose: the real frame width
    // depends on the source's aspect ratio, and a wrong value is worse than none.
    private static string BuildMasterPlaylist(IReadOnlyList<TranscodingOutput> renditions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");

        foreach (var rendition in renditions)
        {
            // Peak bandwidth = (video + audio) kbps, +10% for the rate-control burst, in bits per second.
            var bandwidth = (rendition.Bitrate + AudioBitrateKbps) * 1100;

            sb.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth}");
            sb.AppendLine($"{ResolutionPreset.FolderNames[rendition.Resolution]}/playlist.m3u8");
        }

        return sb.ToString();
    }
}
