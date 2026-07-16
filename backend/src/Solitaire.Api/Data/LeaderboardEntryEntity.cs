using System.ComponentModel.DataAnnotations;

namespace Solitaire.Api.Data;

/// <summary>
/// A server-verified completed game. Rows are only ever written after the engine
/// replay confirmed the win and the score — a client-reported score is never
/// stored directly.
/// </summary>
public sealed class LeaderboardEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    public int Seed { get; set; }

    /// <summary>
    /// The curated/generated level this game corresponds to. Verified: the server
    /// confirms the submitted seed is the canonical seed for (variant, level)
    /// before recording, so a low-difficulty deal cannot be labeled a high level.
    /// Leaderboards rank by the highest level a player has a verified win for.
    /// </summary>
    public int Level { get; set; }

    /// <summary>Engine options bag serialized as JSON (part of the verified identity of the game).</summary>
    [Required]
    public string OptionsJson { get; set; } = "{}";

    /// <summary>The score recomputed by the server-side replay.</summary>
    public int Score { get; set; }

    /// <summary>Client-reported duration; plausibility-checked, not provable.</summary>
    public long TimeMs { get; set; }

    public int MoveCount { get; set; }

    /// <summary>Canonical SHA-256 of (variant, seed, options, moves) — dedupes repeat submissions.</summary>
    [Required]
    [MaxLength(64)]
    public string GameHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
