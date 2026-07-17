using System.ComponentModel.DataAnnotations;
using Solitaire.Engine;

namespace Solitaire.Api.Sync;

/// <summary>
/// A resumable game as it travels between a device and the account. Everything
/// here is untrusted: the move log is replayed through the authoritative engine
/// before it is stored.
/// </summary>
public sealed class SaveDto
{
    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    [Range(1, 1_000_000)]
    public int Level { get; set; }

    public int Seed { get; set; }

    /// <summary>Engine options bag (e.g. drawCount / maxRedeals / suitCount).</summary>
    [Required]
    public Dictionary<string, int> Options { get; set; } = [];

    /// <summary>The moves played so far. Bounded to limit abuse.</summary>
    [Required]
    [MaxLength(5000)]
    public List<MoveDto> Moves { get; set; } = [];

    [Range(0, 10_000)]
    public int HintsUsed { get; set; }

    /// <summary>Accumulated play time so the clock survives a cross-device resume.</summary>
    [Range(0, long.MaxValue)]
    public long ElapsedMs { get; set; }

    /// <summary>Client save time (epoch ms); newest wins when two devices conflict.</summary>
    [Range(0, long.MaxValue)]
    public long UpdatedAt { get; set; }
}

public sealed class ProgressDto
{
    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    [Range(1, 1_000_000)]
    public int CurrentLevel { get; set; }
}

/// <summary>Everything a freshly signed-in device needs to catch up.</summary>
public sealed record SyncStateResponse(
    IReadOnlyList<SaveDto> Saves,
    IReadOnlyList<ProgressDto> Progress);
