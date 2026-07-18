using System.Text.Json;
using Solitaire.Engine;

namespace Solitaire.Api.Leaderboard;

/// <summary>
/// Maps a (variant, level) to its canonical deal seed so the leaderboard can prove
/// a submitted game really is the level it claims to be. The mapping mirrors the
/// frontend's level providers exactly:
/// <list type="bullet">
///   <item>Klondike ships a curated, solver-graded ladder (embedded JSON); only
///   those levels are rankable — deals beyond the ladder are solver-generated on
///   the client and cannot be reproduced here cheaply, so they are unrankable.</item>
///   <item>Spider, FreeCell and Pyramid are endless: level N always uses
///   <c>deriveSeed(variant, N, 0)</c>, a pure function ported from the frontend,
///   so every level is rankable.</item>
/// </list>
/// The win itself is always verified by full engine replay; this only ties the
/// <em>level label</em> to the deal.
/// </summary>
public sealed class LevelRegistry
{
    private static readonly JsonSerializerOptions LadderJson = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<int, int> _klondikeSeeds;

    public LevelRegistry()
    {
        _klondikeSeeds = LoadKlondikeLadder();
    }

    /// <summary>Highest curated Klondike level that can be ranked.</summary>
    public int KlondikeCuratedCount => _klondikeSeeds.Count;

    /// <summary>
    /// The canonical seed for a level, or null when the level is not rankable
    /// (unknown variant, non-positive level, or a Klondike level beyond the ladder).
    /// </summary>
    public int? CanonicalSeed(string variant, int level)
    {
        if (level < 1)
        {
            return null;
        }

        return variant switch
        {
            "klondike" => _klondikeSeeds.TryGetValue(level, out var seed) ? seed : null,
            "spider" => DeriveSeed("spider", level, 0),
            "freecell" => DeriveSeed("freecell", level, 0),
            "pyramid" => DeriveSeed("pyramid", level, 0),
            _ => null,
        };
    }

    /// <summary>
    /// Deterministic seed derivation — a byte-for-byte port of the frontend's
    /// <c>deriveSeed(variant, level, attempt)</c> (see frontend/src/game/levels.ts).
    /// All intermediate arithmetic is 32-bit to match JavaScript's bitwise ops.
    /// </summary>
    internal static int DeriveSeed(string variant, int level, int attempt)
    {
        uint baseSeed = unchecked(
            HashString(variant)
            ^ (uint)(level * 73856093)
            ^ (uint)(attempt * 19349663));
        return unchecked((int)new DeterministicRandom(unchecked((int)baseSeed)).NextUInt32());
    }

    /// <summary>FNV-1a 32-bit hash, matching the frontend's <c>hashString</c>.</summary>
    private static uint HashString(string value)
    {
        uint hash = 2166136261u;
        foreach (char c in value)
        {
            hash = unchecked((hash ^ c) * 16777619u);
        }
        return hash;
    }

    private static Dictionary<int, int> LoadKlondikeLadder()
    {
        using Stream stream =
            typeof(LevelRegistry).Assembly.GetManifestResourceStream(
                "Solitaire.Api.Leaderboard.klondike.levels.json")
            ?? throw new InvalidOperationException("Embedded Klondike level ladder not found.");

        var library = JsonSerializer.Deserialize<CuratedLibrary>(stream, LadderJson)
            ?? throw new InvalidOperationException("Klondike level ladder could not be parsed.");

        return library.Levels.ToDictionary(l => l.Level, l => l.Seed);
    }

    private sealed record CuratedLibrary(List<CuratedLevel> Levels);

    private sealed record CuratedLevel(int Level, int Seed);
}
