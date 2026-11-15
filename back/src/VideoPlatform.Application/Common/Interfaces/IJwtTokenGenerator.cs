// File: IJwtTokenGenerator.cs
// Purpose: Declares JWT/refresh-token generation so AuthEndpoints (Api)
// depends on an abstraction, not a specific JWT library directly.
// Implemented by Infrastructure/Auth/JwtTokenGenerator.cs.
// Note: not part of the original ARCHITECTURE.md interface list — added
// because FLOW.md's token-issuance step (Authentication & Token Flow,
// step 5) needs somewhere to live that both the Api (to issue tokens) and
// unit tests (to fake them) can depend on.
namespace VideoPlatform.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    // Builds a short-lived signed JWT carrying Id/Email claims and an exp timestamp.
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(Guid userId, string email);

    // Builds a long-lived, cryptographically random opaque refresh token string.
    // The caller hashes it before persisting it (see Domain/Entities/RefreshToken.cs).
    string GenerateRefreshToken();
}
