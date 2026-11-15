// File: TokenHasher.cs
// Purpose: One-way hash for refresh tokens before they are stored or
// looked up (see Domain/Entities/RefreshToken.cs) — SHA-256 is sufficient
// here because the input is already a 256-bit random value, not a
// low-entropy secret like a password (which needs a slow, salted hash
// such as PBKDF2/BCrypt instead — that's what ASP.NET Core Identity's own
// password hasher already does for account passwords).
namespace VideoPlatform.Infrastructure.Auth;

using System.Security.Cryptography;
using System.Text;

public static class TokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
