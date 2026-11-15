// File: VideoEndpoints.cs
// Purpose: Maps each HTTP route straight to an Application Command or
// Query and returns its Result. No business logic lives here — every
// endpoint method is a handful of lines (ARCHITECTURE.md section 2.4).
namespace VideoPlatform.Api.Endpoints;

using VideoPlatform.Api.Extensions;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Application.Videos.Commands.ConfirmUpload;
using VideoPlatform.Application.Videos.Commands.RequestUploadUrl;
using VideoPlatform.Application.Videos.Queries.GetVideo;
using VideoPlatform.Application.Videos.Queries.ListVideos;
using VideoPlatform.Domain.Common;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization(): every route below runs only after the JWT
        // middleware has validated the token (FLOW.md steps 9-11).
        var group = app.MapGroup("/api/videos").WithTags("Videos").RequireAuthorization();

        group.MapPost("/upload-url", RequestUploadUrl);
        group.MapPost("/{id:guid}/confirm", ConfirmUpload);
        group.MapGet("/{id:guid}", GetVideo);
        group.MapGet("/", ListVideos);
    }

    // POST /api/videos/upload-url — FLOW.md, Video Upload Flow, step 3.
    private static async Task<IResult> RequestUploadUrl(
        RequestUploadUrlBody body,
        System.Security.Claims.ClaimsPrincipal user,
        ICommandHandler<RequestUploadUrlCommand, Result<RequestUploadUrlResult>> handler,
        CancellationToken cancellationToken)
    {
        var command = new RequestUploadUrlCommand(user.GetUserId(), body.Title, body.FileName);
        var result = await handler.Handle(command, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error);
    }

    // POST /api/videos/{id}/confirm — FLOW.md, Video Upload Flow, step 8.
    private static async Task<IResult> ConfirmUpload(
        Guid id,
        System.Security.Claims.ClaimsPrincipal user,
        ICommandHandler<ConfirmUploadCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ConfirmUploadCommand(id, user.GetUserId()), cancellationToken);

        return result.IsSuccess ? TypedResults.NoContent() : TypedResults.BadRequest(result.Error);
    }

    // GET /api/videos/{id} — polled by useVideoStatus.ts (FLOW.md, Video Streaming Flow, step 2).
    private static async Task<IResult> GetVideo(
        Guid id,
        System.Security.Claims.ClaimsPrincipal user,
        IQueryHandler<GetVideoQuery, Result<VideoDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetVideoQuery(id, user.GetUserId()), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.NotFound();
    }

    // GET /api/videos — DashboardPage.tsx (ARCHITECTURE.md section 6).
    private static async Task<IResult> ListVideos(
        System.Security.Claims.ClaimsPrincipal user,
        IQueryHandler<ListVideosQuery, Result<ListVideosResult>> handler,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        string? q = null)
    {
        var result = await handler.Handle(new ListVideosQuery(user.GetUserId(), page, pageSize, q), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error);
    }
}

// The request body for RequestUploadUrl — OwnerId comes from the JWT, never
// from the body, so a caller can never request an upload URL "as" someone else.
public sealed record RequestUploadUrlBody(string Title, string FileName);
