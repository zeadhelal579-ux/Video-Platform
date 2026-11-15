// File: RefreshTokenTests.cs
// Purpose: Verifies when a refresh token may still be exchanged for a new
// pair — in particular the short grace window that keeps several browser
// tabs, restored at the same instant, from signing each other out.
namespace VideoPlatform.Tests.Domain;

using VideoPlatform.Domain.Entities;
using Xunit;

public class RefreshTokenTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    // Revoke() stamps the real clock, so these tests build tokens relative to the real "now".
    private static RefreshToken NewToken(TimeSpan lifetime)
    {
        var now = DateTimeOffset.UtcNow;
        return RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", now.Add(lifetime), now);
    }

    [Fact]
    public void An_unused_unexpired_token_can_be_rotated()
    {
        var token = NewToken(TimeSpan.FromDays(14));

        Assert.True(token.CanBeRotated(DateTimeOffset.UtcNow, Grace));
    }

    [Fact]
    public void An_expired_token_can_never_be_rotated()
    {
        var token = NewToken(TimeSpan.FromDays(14));

        Assert.False(token.CanBeRotated(DateTimeOffset.UtcNow.AddDays(15), Grace));
    }

    [Fact]
    public void A_token_rotated_a_moment_ago_is_still_accepted_inside_the_grace_window()
    {
        var token = NewToken(TimeSpan.FromDays(14));
        token.Revoke();

        Assert.True(token.CanBeRotated(DateTimeOffset.UtcNow, Grace));
    }

    [Fact]
    public void A_revoked_token_is_rejected_once_the_grace_window_has_passed()
    {
        var token = NewToken(TimeSpan.FromDays(14));
        token.Revoke();

        Assert.False(token.CanBeRotated(DateTimeOffset.UtcNow.AddMinutes(5), Grace));
    }

    [Fact]
    public void A_revoked_token_never_counts_as_active()
    {
        var token = NewToken(TimeSpan.FromDays(14));
        token.Revoke();

        Assert.False(token.IsActive);
    }
}
