// File: ListVideosHandler.cs
// Purpose: Returns one page of the signed-in user's own videos, newest first.
namespace VideoPlatform.Application.Videos.Queries.ListVideos;

using Microsoft.EntityFrameworkCore;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Application.Videos.Queries.GetVideo;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Enums;

public sealed class ListVideosHandler(
    IAppDbContext db,
    IStorageService storage) : IQueryHandler<ListVideosQuery, Result<ListVideosResult>>
{
    private const int MaxSearchLength = 100;
    private const string LikeEscape = "!";

    public async Task<Result<ListVideosResult>> Handle(ListVideosQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
            return Result.Failure<ListVideosResult>("Page must be 1 or greater.");

        if (query.PageSize is < 1 or > 100)
            return Result.Failure<ListVideosResult>("Page size must be between 1 and 100.");

        var offset = (query.Page - 1L) * query.PageSize;
        if (offset > int.MaxValue)
            return Result.Failure<ListVideosResult>("Page is too large for the requested page size.");

        var search = query.Q?.Trim();
        if (search is { Length: > MaxSearchLength })
            return Result.Failure<ListVideosResult>($"Search must be at most {MaxSearchLength} characters.");

        var matchingVideos = db.Videos.AsNoTracking().Where(v => v.OwnerId == query.OwnerId);
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = BuildSearchPattern(search);
            matchingVideos = matchingVideos.Where(v => EF.Functions.Like(v.Title.ToLower(), pattern, LikeEscape));
        }

        var totalCount = await matchingVideos.CountAsync(cancellationToken);

        var page = await matchingVideos
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(v => new VideoDto(
                v.Id,
                v.Title,
                v.Status.ToString(),
                v.Status == VideoStatus.Ready ? storage.GetPublicUrl($"videos/{v.Id}/hls/master.m3u8") : null,
                v.ThumbnailPath is not null ? storage.GetPublicUrl(v.ThumbnailPath) : null,
                null, // the list view never needs the per-job error message — GetVideo covers that detail
                v.CreatedAt))
            .ToList();

        return Result.Success(new ListVideosResult(items, totalCount, query.Page, query.PageSize));
    }

    private static string BuildSearchPattern(string search)
    {
        var escaped = search.ToLowerInvariant()
            .Replace(LikeEscape, "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
