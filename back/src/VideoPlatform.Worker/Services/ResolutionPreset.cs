// File: ResolutionPreset.cs
// Purpose: The width/height/target-bitrate that "1080p / 720p / 480p" maps
// to when actually invoking FFmpeg. Kept as one small static lookup so
// adding a new quality (ARCHITECTURE.md section 7, "Adding a new video
// quality (e.g. 4K)") is a single line here plus one enum value in
// Domain/Enums/Resolution.cs — nothing else in the Api, Domain, or
// frontend needs to change.
// Note: only Height and BitrateKbps drive the encode. The width is derived by
// FFmpeg from the source's aspect ratio (scale=-2:Height), so portrait and
// ultrawide videos are never stretched to 16:9; Width documents the nominal
// 16:9 frame size of each quality.
namespace VideoPlatform.Worker.Services;

using VideoPlatform.Domain.Enums;

public static class ResolutionPreset
{
    public sealed record Preset(int Width, int Height, int BitrateKbps);

    // A rendition may be at most this much taller than the source — enough to
    // absorb odd sizes like 1920x1074 without re-encoding at a pointless higher size.
    private const double UpscaleTolerance = 1.1;

    public static readonly IReadOnlyDictionary<Resolution, string> FolderNames = new Dictionary<Resolution, string>
    {
        [Resolution.R1080p] = "1080p",
        [Resolution.R720p] = "720p",
        [Resolution.R480p] = "480p"
    };

    public static readonly IReadOnlyDictionary<Resolution, Preset> All = new Dictionary<Resolution, Preset>
    {
        [Resolution.R1080p] = new Preset(1920, 1080, 5000),
        [Resolution.R720p] = new Preset(1280, 720, 2800),
        [Resolution.R480p] = new Preset(854, 480, 1400)
    };

    // Chooses which qualities to produce for a source of the given height
    // (null/0 = unknown), highest first. Upscaling a 480p clip to 1080p only
    // burns CPU and storage, so qualities taller than the source are skipped —
    // but the lowest one is always kept so every video has something to play.
    public static IReadOnlyList<KeyValuePair<Resolution, Preset>> SelectFor(int? sourceHeight)
    {
        var highestFirst = All.OrderByDescending(pair => pair.Value.Height).ToList();

        if (sourceHeight is null or <= 0)
            return highestFirst;

        var selected = highestFirst
            .Where(pair => pair.Value.Height <= sourceHeight.Value * UpscaleTolerance)
            .ToList();

        if (selected.Count == 0)
            selected.Add(highestFirst[^1]);

        return selected;
    }
}
