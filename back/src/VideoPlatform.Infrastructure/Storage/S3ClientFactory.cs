// File: S3ClientFactory.cs
// Purpose: The single place that turns StorageOptions into an IAmazonS3
// client, so the "normal" client (server-to-storage calls) and the
// "signing" client (pre-signed URLs for browsers) are configured identically
// except for the endpoint they point at.
namespace VideoPlatform.Infrastructure.Storage;

using Amazon;
using Amazon.S3;

internal static class S3ClientFactory
{
    // Builds a client for the configured storage. serviceUrlOverride, when
    // given, replaces StorageOptions.ServiceUrl (used for the signing client).
    public static IAmazonS3 Create(StorageOptions options, string? serviceUrlOverride = null)
    {
        var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };

        var serviceUrl = string.IsNullOrWhiteSpace(serviceUrlOverride) ? options.ServiceUrl : serviceUrlOverride;

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            // S3-compatible endpoint (MinIO, Cloudflare R2, ...). The SDK cannot
            // derive the SigV4 signing region from such a host name, so it is set explicitly.
            config.ServiceURL = serviceUrl;
            config.AuthenticationRegion = options.Region;
        }
        else
        {
            // Real AWS S3: the region alone decides the endpoint.
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        // No keys configured: fall back to the SDK's default credential chain
        // (environment variables, an EC2/ECS/EKS role, ...), the normal way to
        // run on AWS without long-lived keys in configuration.
        return string.IsNullOrWhiteSpace(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }
}
