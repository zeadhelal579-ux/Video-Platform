// File: RefreshToken.cs
// Purpose: NOT part of the original ARCHITECTURE.md entity list — added
// because FLOW.md's auth flow (steps 5-12) needs a refresh token the Api
// can look up, validate, and revoke, and that requires a durable row
// somewhere. Documented here explicitly so the gap is visible rather than
// silently patched.
// Logic/Approach: Stores a HASH of the token, never the raw value, so a
// database leak alone can never be replayed as a valid refresh token — the
// hashing itself happens in Infrastructure (see Infrastructure/Auth), this
// entity only stores and compares the resulting hash.
namespace VideoPlatform.Domain.Entities;

using VideoPlatform.Domain.Common;

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // Convenience flag: not persisted, computed from the two dates above.
    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    private RefreshToken()
    {
    } // EF Core

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = now;
    }

    // Called by the login/refresh handlers right after a new refresh token string is generated.
    public static RefreshToken Create(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        return new RefreshToken(id, userId, tokenHash, expiresAt, now);
    }

    // Called on logout, or automatically when a refresh token is used once
    // (rotation) — an already-revoked token can never be reused.
    public void Revoke()
    {
        RevokedAt ??= DateTimeOffset.UtcNow;
    }

    // True when this token may be exchanged for a new pair right now.
    // Besides ordinary active tokens, a token that was rotated away only
    // moments ago (inside graceWindow) is still accepted: a browser that
    // restores several tabs at once sends the same cookie from each tab at
    // the same instant, and without this leeway every tab but the first would
    // be signed out by the rotation the first one just performed.
    public bool CanBeRotated(DateTimeOffset now, TimeSpan graceWindow)
    {
        if (now >= ExpiresAt)
            return false;

        return RevokedAt is null || now - RevokedAt.Value <= graceWindow;
    }
}
