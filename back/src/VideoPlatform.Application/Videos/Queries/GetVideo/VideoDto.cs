// File: VideoDto.cs
// Purpose: The exact shape sent to the frontend. Keeping this separate
// from the Video entity means internal fields (like the raw original
// storage path) never leak into an API response by accident
// (ARCHITECTURE.md section 2.2). Mirrored field-for-field by the
// frontend's `Video` TypeScript interface (see TOOLS.md).
namespace VideoPlatform.Application.Videos.Queries.GetVideo;

public sealed record VideoDto(
    Guid Id,
    string Title,
    string Status,
    string? MasterPlaylistUrl,
    string? ThumbnailUrl,
    string? ErrorMessage,
    DateTimeOffset CreatedAt);
