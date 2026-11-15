// File: S3StorageService.cs
// Purpose: Implements IStorageService with the AWS SDK. Because MinIO
// speaks the same S3 API, this one class works unmodified against either —
// only StorageOptions (endpoints, credentials) changes between
// environments (ARCHITECTURE.md section 2.3; TOOLS.md "Storage & Delivery").
// Logic/Approach: Registered as a singleton. It owns a second, "signing"
// client when StorageOptions.PublicServiceUrl is set: pre-signed URLs must
// carry the host the *browser* can reach (the signature covers the host),
// which is different from the internal host the Api uses inside Docker.
namespace VideoPlatform.Infrastructure.Storage;

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using VideoPlatform.Application.Common.Interfaces;

public sealed class S3StorageService : IStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonS3 _signingClient;
    private readonly bool _ownsSigningClient;
    private readonly Protocol _signingProtocol;
    private readonly StorageOptions _options;

    public S3StorageService(IAmazonS3 s3Client, IOptions<StorageOptions> options)
    {
        _options = options.Value;
        _s3Client = s3Client;

        if (string.IsNullOrWhiteSpace(_options.PublicServiceUrl))
        {
            _signingClient = s3Client;
            _ownsSigningClient = false;
        }
        else
        {
            _signingClient = S3ClientFactory.Create(_options, _options.PublicServiceUrl);
            _ownsSigningClient = true;
        }

        // The SDK would otherwise default to https even for a plain-http MinIO endpoint.
        var signingUrl = string.IsNullOrWhiteSpace(_options.PublicServiceUrl) ? _options.ServiceUrl : _options.PublicServiceUrl;
        _signingProtocol = signingUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP
            : Protocol.HTTPS;
    }

    // Generates a time-limited signed PUT URL. This is a local, synchronous
    // computation in the AWS SDK (no network call) — Task.FromResult keeps
    // the interface async without a pointless await.
    public Task<string> GeneratePresignedUploadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            Protocol = _signingProtocol
        };

        return Task.FromResult(_signingClient.GetPreSignedURL(request));
    }

    // Pulls one object down to a local path — used by the Worker to fetch
    // the raw upload before transcoding.
    public async Task DownloadAsync(string objectKey, string destinationPath, CancellationToken cancellationToken = default)
    {
        using var response = await _s3Client.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);
        await using var fileStream = File.Create(destinationPath);
        await response.ResponseStream.CopyToAsync(fileStream, cancellationToken);
    }

    // Pushes one object up — used by the Worker once per generated HLS
    // file (.ts segments + .m3u8 playlists).
    public async Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType
        };
        request.Headers.CacheControl = BuildCacheControl(objectKey);

        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    // Builds the CDN-fronted URL the frontend player and thumbnails load
    // (FLOW.md, Video Streaming Flow, step 3).
    public string GetPublicUrl(string objectKey) => $"{_options.PublicBaseUrl.TrimEnd('/')}/{objectKey}";

    // Releases the signing client when this class created it; the shared
    // client belongs to the DI container, which disposes it itself.
    public void Dispose()
    {
        if (_ownsSigningClient)
            _signingClient.Dispose();
    }

    // HLS segments never change once written, so a CDN may keep them "forever";
    // playlists get a short lifetime as a safety net.
    private static string BuildCacheControl(string objectKey) =>
        objectKey.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
            ? "public, max-age=31536000, immutable"
            : "public, max-age=300";
}
