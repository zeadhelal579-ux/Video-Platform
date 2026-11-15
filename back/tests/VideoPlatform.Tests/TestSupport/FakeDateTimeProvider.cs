// File: FakeDateTimeProvider.cs
// Purpose: A deterministic clock for assertions that check timestamps
// exactly, instead of a fuzzy "close to DateTimeOffset.UtcNow" check.
namespace VideoPlatform.Tests.TestSupport;

using VideoPlatform.Application.Common.Interfaces;

public sealed class FakeDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
