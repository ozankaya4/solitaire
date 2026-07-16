using Solitaire.Api.Leaderboard;
using Xunit;

namespace Solitaire.Api.Tests;

/// <summary>
/// Locks the server's level->seed mapping to the frontend's. The Spider expected
/// values were produced by the frontend's <c>deriveSeed(variant, level, 0)</c>;
/// if the port drifts, legitimate Spider wins would stop ranking.
/// </summary>
public class LevelRegistryTests
{
    private readonly LevelRegistry _registry = new();

    [Theory]
    [InlineData(1, 2135846407)]
    [InlineData(2, 1660380619)]
    [InlineData(10, -1570760447)]
    [InlineData(999, -1978559148)]
    public void SpiderCanonicalSeed_MatchesFrontendDeriveSeed(int level, int expected)
    {
        Assert.Equal(expected, _registry.CanonicalSeed("spider", level));
    }

    [Theory]
    [InlineData(31, 2)] // curated ladder: level 31 -> seed 2
    [InlineData(32, 4)] // curated ladder: level 32 -> seed 4
    public void KlondikeCanonicalSeed_UsesCuratedLadder(int level, int expectedSeed)
    {
        Assert.Equal(expectedSeed, _registry.CanonicalSeed("klondike", level));
    }

    [Fact]
    public void KlondikeBeyondLadder_IsUnrankable()
    {
        Assert.True(_registry.KlondikeCuratedCount >= 1);
        Assert.Null(_registry.CanonicalSeed("klondike", _registry.KlondikeCuratedCount + 1));
        Assert.Null(_registry.CanonicalSeed("klondike", 100_000));
    }

    [Theory]
    [InlineData("klondike", 0)]
    [InlineData("spider", -3)]
    [InlineData("freecell", 5)] // no server engine / provider
    public void NonPositiveOrUnknown_IsUnrankable(string variant, int level)
    {
        Assert.Null(_registry.CanonicalSeed(variant, level));
    }
}
