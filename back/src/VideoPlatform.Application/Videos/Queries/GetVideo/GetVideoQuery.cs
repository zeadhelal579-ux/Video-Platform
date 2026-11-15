// File: GetVideoQuery.cs
// Purpose: Input for "fetch this one video's current status" — the query
// useVideoStatus.ts polls on an interval until status is Ready or Failed
// (FLOW.md, Video Streaming Flow, step 2).
namespace VideoPlatform.Application.Videos.Queries.GetVideo;

public sealed record GetVideoQuery(Guid VideoId, Guid RequesterId);
