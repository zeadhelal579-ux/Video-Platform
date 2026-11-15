// File: ListVideosQuery.cs
// Purpose: Input for "list my videos" — paginated, filtered to the
// signed-in user's own videos (ARCHITECTURE.md section 2.2).
namespace VideoPlatform.Application.Videos.Queries.ListVideos;

using VideoPlatform.Application.Videos.Queries.GetVideo;

public sealed record ListVideosQuery(Guid OwnerId, int Page = 1, int PageSize = 20, string? Q = null);

// A simple paging envelope: the items for this page plus enough metadata
// for the frontend to render pagination controls.
public sealed record ListVideosResult(
    IReadOnlyList<VideoDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
