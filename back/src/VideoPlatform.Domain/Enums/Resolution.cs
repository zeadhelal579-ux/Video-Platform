// File: Resolution.cs
// Purpose: The output quality renditions the Worker produces for every
// video. Adding a new quality (e.g. 4K) is a one-line addition here plus
// one new case in FFmpegTranscodingService — nothing else in the Api,
// Domain, or frontend needs to change (see ARCHITECTURE.md section 7,
// "Adding a new video quality").
namespace VideoPlatform.Domain.Enums;

public enum Resolution
{
    R480p = 0,
    R720p = 1,
    R1080p = 2
}
