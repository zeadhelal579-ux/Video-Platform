// File: RequestUploadUrlHandler.cs
// Purpose: Creates the Video row at Pending and returns a pre-signed
// upload URL (FLOW.md, Video Upload Flow, step 4).
// Logic/Approach: Everything the client sends (title, file name) is
// untrusted input. The title is length-checked, and the file name is
// reduced to a safe "name.ext" before it becomes part of the storage key,
// so a crafted name can never inject path segments ("../") or oversized
// keys into storage or into the Worker's local file handling.
namespace VideoPlatform.Application.Videos.Commands.RequestUploadUrl;

using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Application.Common.Messaging;
using VideoPlatform.Domain.Common;
using VideoPlatform.Domain.Entities;

public sealed class RequestUploadUrlHandler(
    IAppDbContext db,
    IStorageService storage,
    IDateTimeProvider clock) : ICommandHandler<RequestUploadUrlCommand, Result<RequestUploadUrlResult>>
{
    // A pre-signed PUT URL is only valid for this long — short enough to
    // limit exposure if the URL ever leaked, long enough for a slow upload to finish.
    private static readonly TimeSpan UploadUrlExpiry = TimeSpan.FromMinutes(30);

    // Must match the column length configured for Video.Title.
    public const int MaxTitleLength = 200;

    private const int MaxBaseNameLength = 100;
    private const int MaxExtensionLength = 10; // includes the leading dot
    private const string FallbackBaseName = "video";

    // Validates the input, builds the new video, persists it, then asks storage for the signed URL.
    public async Task<Result<RequestUploadUrlResult>> Handle(RequestUploadUrlCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
            return Result.Failure<RequestUploadUrlResult>("Title is required.");

        if (command.Title.Trim().Length > MaxTitleLength)
            return Result.Failure<RequestUploadUrlResult>($"Title must be at most {MaxTitleLength} characters.");

        if (string.IsNullOrWhiteSpace(command.FileName))
            return Result.Failure<RequestUploadUrlResult>("File name is required.");

        var videoId = Guid.NewGuid();

        // Everything for one video lives under the same "videos/{id}/..." prefix,
        // which is also what makes "videos/{id}/hls/" a natural home for the
        // Worker's output later (see FFmpegTranscodingService).
        var objectKey = $"videos/{videoId}/original/{BuildSafeFileName(command.FileName)}";

        var video = Video.Create(videoId, command.Title, command.OwnerId, objectKey, clock.UtcNow);

        db.Videos.Add(video);
        await db.SaveChangesAsync(cancellationToken);

        var uploadUrl = await storage.GeneratePresignedUploadUrlAsync(objectKey, UploadUrlExpiry, cancellationToken);

        return Result.Success(new RequestUploadUrlResult(videoId, uploadUrl));
    }

    // Reduces an arbitrary client-supplied file name to "base.ext" made only of
    // ASCII letters, digits, '-' and '_' (plus one short extension). Anything
    // else — path separators, spaces, non-ASCII characters — is dropped, and an
    // empty result falls back to "video", so the key is always valid and short.
    internal static string BuildSafeFileName(string rawFileName)
    {
        // Only the last path segment counts: "..\..\evil.mp4" must never influence the key.
        var normalized = rawFileName.Replace('\\', '/');
        var lastSegment = normalized[(normalized.LastIndexOf('/') + 1)..];

        var extension = new string(Path.GetExtension(lastSegment)
            .Where(c => c == '.' || char.IsAsciiLetterOrDigit(c))
            .ToArray())
            .ToLowerInvariant();

        if (extension.Length > MaxExtensionLength)
            extension = extension[..MaxExtensionLength];

        // A lone "." is not an extension.
        if (extension == ".")
            extension = string.Empty;

        var baseName = new string(Path.GetFileNameWithoutExtension(lastSegment)
            .Select(c => c == ' ' ? '_' : c)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            .ToArray());

        if (baseName.Length > MaxBaseNameLength)
            baseName = baseName[..MaxBaseNameLength];

        if (baseName.Length == 0)
            baseName = FallbackBaseName;

        return baseName + extension;
    }
}
