// File: StorageOptions.cs
// Purpose: Binds the "Storage" section of appsettings.json / environment
// variables — bucket name, endpoints, region and the credentials needed to
// construct IAmazonS3 (registered in DependencyInjection.cs).
namespace VideoPlatform.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    // Endpoint the Api and Worker use for their own server-to-storage calls
    // (download / upload). Set it for MinIO or any S3-compatible service
    // (e.g. "http://minio:9000" inside Docker); leave it empty for real AWS
    // S3, where the SDK then uses the regional AWS endpoint.
    public string ServiceUrl { get; set; } = string.Empty;

    // The endpoint embedded in pre-signed upload URLs. Browsers call it
    // directly, so it must be reachable from the user's machine. Only needed
    // when it differs from ServiceUrl — e.g. inside Docker, where the Api
    // reaches MinIO as "http://minio:9000" but users reach it as
    // "https://media.example.com". Empty = sign with ServiceUrl / AWS.
    public string PublicServiceUrl { get; set; } = string.Empty;

    // AWS region (e.g. "eu-central-1"). For S3-compatible endpoints it is only
    // the SigV4 signing region: "us-east-1" for MinIO, "auto" for Cloudflare R2.
    public string Region { get; set; } = "us-east-1";

    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    // The base URL players load HLS files from. In production this is the
    // CDN domain in front of the bucket; locally it is the MinIO address
    // including the bucket, e.g. "http://localhost:9000/videoplatform".
    public string PublicBaseUrl { get; set; } = string.Empty;

    // MinIO needs path-style URLs (http://host/bucket/key); real AWS S3
    // prefers virtual-hosted style — this one flag is the only thing that
    // changes between the two (TOOLS.md, "MinIO"/"AWS S3").
    public bool ForcePathStyle { get; set; } = true;
}
