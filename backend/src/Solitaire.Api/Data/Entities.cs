using System.ComponentModel.DataAnnotations;

namespace Solitaire.Api.Data;

/// <summary>A per-account, per-variant unfinished game (migrated from a guest, later synced).</summary>
public sealed class GameSaveEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to <see cref="ApplicationUser"/>.</summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    public int Level { get; set; }

    public int Seed { get; set; }

    /// <summary>Engine options bag serialized as JSON.</summary>
    [Required]
    public string OptionsJson { get; set; } = "{}";

    /// <summary>Move list serialized as JSON.</summary>
    [Required]
    public string MovesJson { get; set; } = "[]";

    public int HintsUsed { get; set; }

    /// <summary>
    /// Accumulated play time, so the clock keeps running when a game is resumed on
    /// another device (a reset clock would make a later win look impossibly fast to
    /// the leaderboard's plausibility check).
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Client-supplied save time. Used to resolve conflicts between devices
    /// (newest wins), so it is compared, never trusted as a wall clock.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Lifetime, per-account, per-variant statistics.</summary>
public sealed class PlayerStatEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    public int GamesPlayed { get; set; }

    public int Wins { get; set; }

    public long? BestTimeMs { get; set; }

    /// <summary>
    /// The player's current level in this variant, synced across devices. Only ever
    /// moves forward (the server keeps the max), so a stale device cannot roll
    /// progress back.
    /// </summary>
    public int CurrentLevel { get; set; } = 1;
}
