// File: FakeStorageService.cs
// Purpose: A fake storage backend for handler tests — never touches real
// S3/MinIO, just records what it was asked to do and returns predictable URLs.
namespace VideoPlatform.Tests.TestSupport;

using VideoPlatform.Application.Common.Interfaces;

public sealed class FakeStorageService : IStorageService
{
    public List<string> RequestedUploadUrlKeys { get; } = [];

    public Task<string> GeneratePresignedUploadUrlAsync(string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        RequestedUploadUrlKeys.Add(objectKey);
        return Task.FromResult($"https://fake-storage.test/{objectKey}?signed=true");
    }

    public Task DownloadAsync(string objectKey, string destinationPath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by the Application-layer tests that use this fake.");

    public Task UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by the Application-layer tests that use this fake.");

    public string GetPublicUrl(string objectKey) => $"https://cdn.fake-storage.test/{objectKey}";
}
