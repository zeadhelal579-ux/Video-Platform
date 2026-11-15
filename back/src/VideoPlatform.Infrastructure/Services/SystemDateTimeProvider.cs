// File: SystemDateTimeProvider.cs
// Purpose: The real implementation of IDateTimeProvider — deliberately
// trivial; its only job is to exist so unit tests can substitute a fake
// clock instead (see VideoPlatform.Tests).
namespace VideoPlatform.Infrastructure.Services;

using VideoPlatform.Application.Common.Interfaces;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
