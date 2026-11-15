// File: ResolutionPresetTests.cs
// Purpose: Sanity-checks the FFmpeg resolution lookup table every
// transcoded rendition is built from (Worker/Services/ResolutionPreset.cs).
// Note: does not exercise FFmpegTranscodingService itself, since that
// requires an actual ffmpeg binary on PATH — out of scope for a fast unit
// suite. See the final report for the recommended follow-up integration test.
namespace VideoPlatform.Tests.Worker;

using VideoPlatform.Domain.Enums;
using VideoPlatform.Worker.Services;
using Xunit;

public class ResolutionPresetTests
{
    [Theory]
    [InlineData(Resolution.R1080p)]
    [InlineData(Resolution.R720p)]
    [InlineData(Resolution.R480p)]
    public void Every_resolution_has_a_preset_and_a_folder_name(Resolution resolution)
    {
        Assert.True(ResolutionPreset.All.ContainsKey(resolution));
        Assert.True(ResolutionPreset.FolderNames.ContainsKey(resolution));

        var preset = ResolutionPreset.All[resolution];
        Assert.True(preset.Width > 0);
        Assert.True(preset.Height > 0);
        Assert.True(preset.BitrateKbps > 0);
    }

    [Fact]
    public void Higher_resolutions_get_a_higher_target_bitrate()
    {
        var r1080 = ResolutionPreset.All[Resolution.R1080p].BitrateKbps;
        var r720 = ResolutionPreset.All[Resolution.R720p].BitrateKbps;
        var r480 = ResolutionPreset.All[Resolution.R480p].BitrateKbps;

        Assert.True(r1080 > r720);
        Assert.True(r720 > r480);
    }

    [Theory]
    [InlineData(null, "1080p,720p,480p")]  // unknown height (probe failed): produce everything
    [InlineData(0, "1080p,720p,480p")]
    [InlineData(2160, "1080p,720p,480p")]
    [InlineData(1080, "1080p,720p,480p")]
    [InlineData(1074, "1080p,720p,480p")]  // slightly short of 1080: inside the 10% tolerance
    [InlineData(720, "720p,480p")]
    [InlineData(540, "480p")]
    [InlineData(480, "480p")]
    [InlineData(240, "480p")]              // tiny source: the lowest quality is always kept
    public void SelectFor_skips_qualities_taller_than_the_source_and_orders_highest_first(int? sourceHeight, string expectedFolders)
    {
        var selected = ResolutionPreset.SelectFor(sourceHeight)
            .Select(pair => ResolutionPreset.FolderNames[pair.Key]);

        Assert.Equal(expectedFolders, string.Join(",", selected));
    }
}

