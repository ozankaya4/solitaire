using Xunit;

namespace Solitaire.Engine.Tests;

public class SolitaireEnginesTests
{
    [Fact]
    public void Registry_ExposesKlondikeAndSpider()
    {
        Assert.Contains("klondike", SolitaireEngines.Variants);
        Assert.Contains("spider", SolitaireEngines.Variants);
    }

    [Theory]
    [InlineData("klondike")]
    [InlineData("spider")]
    [InlineData("KLONDIKE")] // case-insensitive
    public void For_ResolvesKnownVariants(string variant)
    {
        var engine = SolitaireEngines.For(variant);
        Assert.Equal(variant.ToLowerInvariant(), engine.Variant);
    }

    [Fact]
    public void For_UnknownVariant_Throws() =>
        Assert.Throws<ArgumentException>(() => SolitaireEngines.For("freecell"));

    [Fact]
    public void UniformReplay_VerifiesAKlondikeGame()
    {
        // The exact path the API takes: resolve by variant id, replay a portable game.
        var moves = new List<MoveDto> { new("Draw"), new("Draw") };
        var options = new Dictionary<string, int> { ["drawCount"] = 1 };
        var outcome = SolitaireEngines.For("klondike").Replay(new GameDefinition(1, options, moves));

        Assert.True(outcome.AllMovesLegal);
        Assert.False(outcome.Won);
    }

    [Fact]
    public void UniformReplay_VerifiesASpiderGame()
    {
        var moves = new List<MoveDto> { new("Deal") };
        var options = new Dictionary<string, int> { ["suitCount"] = 4 };
        var outcome = SolitaireEngines.For("spider").Replay(new GameDefinition(1, options, moves));

        Assert.True(outcome.AllMovesLegal);
        Assert.Equal(499, outcome.Score); // 500 - 1 for the deal
    }

    [Fact]
    public void UniformReplay_DetectsAnIllegalKlondikeMove()
    {
        // Recycle with a full stock is illegal at move index 0.
        var moves = new List<MoveDto> { new("Recycle") };
        var options = new Dictionary<string, int> { ["drawCount"] = 1 };
        var outcome = SolitaireEngines.For("klondike").Replay(new GameDefinition(1, options, moves));

        Assert.False(outcome.AllMovesLegal);
        Assert.Equal(0, outcome.FirstIllegalMoveIndex);
    }
}
