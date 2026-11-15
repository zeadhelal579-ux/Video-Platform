// File: ApplicationUser.cs
// Purpose: The actual Identity-managed user row (Email, PasswordHash,
// SecurityStamp, etc. all inherited from IdentityUser<Guid>).
// Logic/Approach: Lives in Infrastructure, NOT Domain, because Domain must
// stay framework-free (ARCHITECTURE.md section 2.1) — ASP.NET Core
// Identity is exactly the kind of framework dependency
// Domain/Entities/User.cs explicitly avoids. Domain.User models the same
// person conceptually (profile fields only); this class is the row
// ASP.NET Core Identity's UserManager actually operates on.
namespace VideoPlatform.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    // The one extra profile field the platform needs beyond what Identity already provides.
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
