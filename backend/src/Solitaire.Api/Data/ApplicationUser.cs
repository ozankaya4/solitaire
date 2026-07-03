using Microsoft.AspNetCore.Identity;

namespace Solitaire.Api.Data;

/// <summary>
/// The application user. Extends the default Identity user (string GUID key,
/// username, email, hashed password, lockout, security stamp). Kept minimal now;
/// email-confirmation / reset flows can be layered on later using the token
/// providers already registered.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>When the account was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
