// File: MarkVideoReadyCommand.cs
// Purpose: Input describing every rendition FFmpeg produced, used to mark
// a video Ready once transcoding finishes successfully
// (FLOW.md, Video Processing Flow, step 6).
namespace VideoPlatform.Application.Videos.Commands.MarkVideoReady;

using VideoPlatform.Domain.Enums;

public sealed record RenditionInput(Resolution Resolution, string M3u8Path, int Bitrate);

public sealed record MarkVideoReadyCommand(Guid VideoId, IReadOnlyList<RenditionInput> Renditions);
