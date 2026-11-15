// File: IStorageService.cs
// Purpose: Declares every storage operation the platform needs
// (GeneratePresignedUploadUrl, Download, Upload, GetPublicUrl) without
// saying whether the real implementation talks to MinIO or AWS S3 — both
// the Api's upload-url handler and the Worker's processing consumer
// depend on this interface only (ARCHITECTURE.md section 2.2).
namespace VideoPlatform.Application.Common.Interfaces;

public interface IStorageService
{
    // Used by RequestUploadUrlHandler: returns a time-limited signed PUT
    // URL the browser can upload directly to, bypassing the Api entirely.
    Task<string> GeneratePresignedUploadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default);

    // Used by the Worker to pull the raw upload down to a local temp path before transcoding.
    Task DownloadAsync(string objectKey, string destinationPath, CancellationToken cancellationToken = default);

    // Used by the Worker to push every generated HLS file (.ts segments +
    // .m3u8 playlists) back to storage, one call per file.
    Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    // Used by GetVideoHandler/ListVideosHandler to build the public,
    // CDN-fronted URL the frontend player and thumbnail <img> load.
    string GetPublicUrl(string objectKey);
}
