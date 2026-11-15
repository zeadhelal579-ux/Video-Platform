// File: User.cs
// Purpose: Identity/profile fields only (email, display name).
// Logic/Approach: Password hashing and credential verification are
// delegated to ASP.NET Core Identity, which lives in the Infrastructure
// layer (see Infrastructure/Identity/ApplicationUser.cs) — this keeps the
// Domain layer free of any authentication-framework dependency, per
// ARCHITECTURE.md section 2.1.
namespace VideoPlatform.Domain.Entities;

using VideoPlatform.Domain.Common;

public sealed class User : Entity
{
    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core needs a parameterless constructor to materialize entities.
    private User()
    {
    }

    private User(Guid id, string email, string displayName, DateTimeOffset createdAt) : base(id)
    {
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
    }

    // The only supported way to build a User, so every invariant
    // (non-empty email/display name) is enforced in exactly one place.
    public static User Create(Guid id, string email, string displayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new User(id, email.Trim().ToLowerInvariant(), displayName.Trim(), now);
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        DisplayName = displayName.Trim();
    }
}
