using Xunit;

namespace Solitaire.Engine.Tests;

public class SolitaireEnginesTests
{
    [Fact]
    public void Registry_ExposesEveryImplementedVariant()
    {
        Assert.Contains("klondike", SolitaireEngines.Variants);
        Assert.Contains("spider", SolitaireEngines.Variants);
        Assert.Contains("freecell", SolitaireEngines.Variants);
        Assert.Contains("pyramid", SolitaireEngines.Variants);
    }

    [Theory]
    [InlineData("klondike")]
    [InlineData("spider")]
    [InlineData("freecell")]
    [InlineData("pyramid")]
    [InlineData("KLONDIKE")] // case-insensitive
    public void For_ResolvesKnownVariants(string variant)
    {
        var engine = SolitaireEngines.For(variant);
        Assert.Equal(variant.ToLowerInvariant(), engine.Variant);
    }

    [Fact]
    public void For_UnknownVariant_Throws() =>
        Assert.Throws<ArgumentException>(() => SolitaireEngines.For("tripeaks"));

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

    [Fact]
    public void UniformReplay_VerifiesAFreeCellGame()
    {
        // Move column 0's top card into a free cell — legal in any FreeCell deal.
        var moves = new List<MoveDto> { new("TableauToFreeCell", Source: 0, Destination: 0) };
        var outcome = SolitaireEngines.For("freecell").Replay(new GameDefinition(1, new Dictionary<string, int>(), moves));

        Assert.True(outcome.AllMovesLegal);
        Assert.False(outcome.Won);
    }

    [Fact]
    public void UniformReplay_DetectsAnIllegalFreeCellMove()
    {
        // Free-celling into an already-occupied cell twice in a row is illegal.
        var moves = new List<MoveDto>
        {
            new("TableauToFreeCell", Source: 0, Destination: 0),
            new("TableauToFreeCell", Source: 1, Destination: 0),
        };
        var outcome = SolitaireEngines.For("freecell").Replay(new GameDefinition(1, new Dictionary<string, int>(), moves));

        Assert.False(outcome.AllMovesLegal);
        Assert.Equal(1, outcome.FirstIllegalMoveIndex);
    }

    [Fact]
    public void UniformReplay_VerifiesAPyramidGame()
    {
        // Drawing a card is legal against any fresh Pyramid deal.
        var moves = new List<MoveDto> { new("Draw") };
        var outcome = SolitaireEngines.For("pyramid").Replay(new GameDefinition(1, new Dictionary<string, int>(), moves));

        Assert.True(outcome.AllMovesLegal);
        Assert.False(outcome.Won);
    }

    [Fact]
    public void UniformReplay_DetectsAnIllegalPyramidMove()
    {
        // Recycling with a full stock (nothing drawn yet) is illegal at move index 0.
        var moves = new List<MoveDto> { new("Recycle") };
        var outcome = SolitaireEngines.For("pyramid").Replay(new GameDefinition(1, new Dictionary<string, int>(), moves));

        Assert.False(outcome.AllMovesLegal);
        Assert.Equal(0, outcome.FirstIllegalMoveIndex);
    }
}
