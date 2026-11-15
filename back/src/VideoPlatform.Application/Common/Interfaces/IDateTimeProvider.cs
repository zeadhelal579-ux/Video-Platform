// File: IDateTimeProvider.cs
// Purpose: Abstracts "now" so Application handlers never call
// DateTimeOffset.UtcNow directly — this is what makes their status-
// transition logic deterministically unit-testable (see VideoPlatform.Tests).
// Note: not part of the original ARCHITECTURE.md interface list — a small,
// standard testability addition.
namespace VideoPlatform.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
