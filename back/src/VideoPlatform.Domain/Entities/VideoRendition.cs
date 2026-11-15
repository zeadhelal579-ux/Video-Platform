// File: VideoRendition.cs
// Purpose: One row per output quality, written once processing succeeds
// (a Ready video has exactly three of these: 1080p, 720p, 480p).
namespace VideoPlatform.Domain.Entities;

using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;

public sealed class VideoRendition : Entity
{
    public Guid VideoId { get; private set; }
    public Resolution Resolution { get; private set; }
    public string M3u8Path { get; private set; } = null!;
    public int Bitrate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private VideoRendition()
    {
    } // EF Core

    private VideoRendition(Guid id, Guid videoId, Resolution resolution, string m3u8Path, int bitrate, DateTimeOffset now)
        : base(id)
    {
        VideoId = videoId;
        Resolution = resolution;
        M3u8Path = m3u8Path;
        Bitrate = bitrate;
        CreatedAt = now;
    }

    // Called once by MarkVideoReadyHandler for each of the three renditions FFmpeg produced.
    public static VideoRendition Create(Guid id, Guid videoId, Resolution resolution, string m3u8Path, int bitrate, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(m3u8Path))
            throw new ArgumentException("Variant playlist path is required.", nameof(m3u8Path));

        if (bitrate <= 0)
            throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate, "Bitrate must be positive.");

        return new VideoRendition(id, videoId, resolution, m3u8Path, bitrate, now);
    }
}
