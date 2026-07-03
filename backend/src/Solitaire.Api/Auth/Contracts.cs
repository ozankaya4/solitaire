using System.ComponentModel.DataAnnotations;
using Solitaire.Engine;

namespace Solitaire.Api.Auth;

/// <summary>Registration payload. Optionally carries a guest's local data to migrate.</summary>
public sealed class RegisterRequest
{
    [Required]
    [StringLength(32, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.\-]+$", ErrorMessage = "Username may contain letters, digits, and . _ -")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional one-time migration of the guest's local stats/saves.</summary>
    public GuestDataDto? GuestData { get; set; }
}

/// <summary>Login payload — accepts either a username or an email.</summary>
public sealed class LoginRequest
{
    [Required]
    [StringLength(256)]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

/// <summary>The guest's exported local data. Size-bounded to limit abuse.</summary>
public sealed class GuestDataDto
{
    [MaxLength(16)]
    public List<GuestStatDto> Stats { get; set; } = [];

    [MaxLength(16)]
    public List<GuestSaveDto> Saves { get; set; } = [];
}

public sealed class GuestStatDto
{
    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public int GamesPlayed { get; set; }

    [Range(0, 1_000_000)]
    public int Wins { get; set; }

    [Range(0, long.MaxValue)]
    public long? BestTimeMs { get; set; }
}

public sealed class GuestSaveDto
{
    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    [Range(1, 1_000_000)]
    public int Level { get; set; }

    public int Seed { get; set; }

    public Dictionary<string, int> Options { get; set; } = [];

    /// <summary>The move list; replayed through the engine to confirm it is a legal game.</summary>
    [MaxLength(5000)]
    public List<MoveDto> Moves { get; set; } = [];

    [Range(0, 10_000)]
    public int HintsUsed { get; set; }
}

/// <summary>Public view of the signed-in user.</summary>
public sealed record UserResponse(string Id, string? Username, string? Email);
