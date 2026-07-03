using System.ComponentModel.DataAnnotations;
using Solitaire.Engine;

namespace Solitaire.Api.Leaderboard;

/// <summary>
/// A completed-game submission. Everything here is untrusted: the server replays
/// the move log through the authoritative engine and recomputes the result.
/// </summary>
public sealed class SubmitGameRequest
{
    [Required]
    [MaxLength(32)]
    public string Variant { get; set; } = string.Empty;

    public int Seed { get; set; }

    /// <summary>Engine options bag (e.g. drawCount / maxRedeals / suitCount).</summary>
    [Required]
    public Dictionary<string, int> Options { get; set; } = [];

    /// <summary>The full move log to replay. Bounded to limit abuse.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(10_000)]
    public List<MoveDto> Moves { get; set; } = [];

    /// <summary>The score the client claims; must match the replay exactly.</summary>
    [Range(0, 1_000_000)]
    public int ClaimedScore { get; set; }

    /// <summary>Wall-clock duration the client claims; plausibility-checked.</summary>
    [Range(1, long.MaxValue)]
    public long ClaimedTimeMs { get; set; }
}

public sealed record SubmitGameResponse(int Score, long TimeMs, int Rank);

public sealed record LeaderboardRow(int Rank, string Username, int Score, long TimeMs);

public sealed record LeaderboardResponse(
    string Variant,
    IReadOnlyList<LeaderboardRow> Top,
    int? PlayerRank,
    int? PlayerBestScore);
