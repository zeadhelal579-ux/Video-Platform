// File: RequestUploadUrlCommand.cs
// Purpose: Input for "I want to upload a video" — the first step of the
// upload flow (FLOW.md, Video Upload Flow, steps 2-4).
namespace VideoPlatform.Application.Videos.Commands.RequestUploadUrl;

public sealed record RequestUploadUrlCommand(Guid OwnerId, string Title, string FileName);

// The exact shape returned to the frontend: the new video's id plus the
// pre-signed URL it must PUT the raw bytes to.
public sealed record RequestUploadUrlResult(Guid VideoId, string UploadUrl);
