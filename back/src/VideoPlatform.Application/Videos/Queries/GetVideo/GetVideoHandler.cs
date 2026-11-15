// File: GetVideoHandler.cs
// Purpose: Reads a single video, builds its public playlist/thumbnail
// URLs once it's Ready, and surfaces a generic failure message once it's Failed.
namespace VideoPlatform.Application.Videos.Queries.GetVideo;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;

public sealed class GetVideoHandler(
    IAppDbContext db,
    IStorageService storage) : IQueryHandler<GetVideoQuery, Result<VideoDto>>
{
    private const string FailedVideoMessage =
        "We couldn't process this video. Please try uploading it again, or use a different file format.";

    public async Task<Result<VideoDto>> Handle(GetVideoQuery query, CancellationToken cancellationToken = default)
    {
        var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == query.VideoId, cancellationToken);
        if (video is null)
            return Result.Failure<VideoDto>("Video not found.");

        if (video.OwnerId != query.RequesterId)
            return Result.Failure<VideoDto>("You do not have permission to view this video.");

        // The master playlist only exists in storage once processing succeeded —
        // building the URL before that would point at a file that doesn't exist yet.
        string? masterPlaylistUrl = video.Status == VideoStatus.Ready
            ? storage.GetPublicUrl($"videos/{video.Id}/hls/master.m3u8")
            : null;

        string? thumbnailUrl = video.ThumbnailPath is not null
            ? storage.GetPublicUrl(video.ThumbnailPath)
            : null;

        // The raw ProcessingJob.ErrorMessage can contain FFmpeg output, storage
        // endpoints and file paths. That detail is for operators (database and
        // logs) only — the owner gets a generic, actionable message instead.
        string? errorMessage = video.Status == VideoStatus.Failed ? FailedVideoMessage : null;

        var dto = new VideoDto(
            video.Id, video.Title, video.Status.ToString(), masterPlaylistUrl, thumbnailUrl, errorMessage, video.CreatedAt);

        return Result.Success(dto);
    }
}
