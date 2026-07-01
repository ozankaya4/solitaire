using Xunit;
using static Solitaire.Engine.Tests.TestStates;

namespace Solitaire.Engine.Tests;

public class ScoringTests
{
    [Fact]
    public void WasteToFoundation_ScoresTen()
    {
        var state = State(waste: [Card(Suit.Hearts, 1)]);
        Klondike.TryApplyMove(state, Move.WasteToFoundation(), out var next, out int delta);
        Assert.Equal(10, delta);
        Assert.Equal(10, next.Score);
    }

    [Fact]
    public void WasteToTableau_ScoresFive()
    {
        var state = State(waste: [Card(Suit.Spades, 13)]);
        Klondike.TryApplyMove(state, Move.WasteToTableau(0), out var next, out int delta);
        Assert.Equal(5, delta);
        Assert.Equal(5, next.Score);
    }

    [Fact]
    public void TableauToFoundation_ScoresTen_PlusFiveWhenFlipping()
    {
        var flip = State(tableau: [Pile(1, Card(Suit.Clubs, 5), Card(Suit.Hearts, 1))]);
        Klondike.TryApplyMove(flip, Move.TableauToFoundation(0), out _, out int flipDelta);
        Assert.Equal(15, flipDelta);

        var noFlip = State(tableau: [FaceUp(Card(Suit.Hearts, 1))]);
        Klondike.TryApplyMove(noFlip, Move.TableauToFoundation(0), out _, out int noFlipDelta);
        Assert.Equal(10, noFlipDelta);
    }

    [Fact]
    public void FoundationToTableau_ScoresMinusFifteen()
    {
        var state = State(
            foundations: [0, 0, 3, 0],
            tableau: [FaceUp(Card(Suit.Spades, 4))],
            score: 100);
        Klondike.TryApplyMove(state, Move.FoundationToTableau(Suit.Hearts, 0), out var next, out int delta);
        Assert.Equal(-15, delta);
        Assert.Equal(85, next.Score);
    }

    [Fact]
    public void Recycle_DrawOne_ScoresMinusHundred_ClampedAtZero()
    {
        var state = State(options: GameOptions.DrawOne, waste: [Card(Suit.Hearts, 9)], score: 30);
        Klondike.TryApplyMove(state, Move.Recycle(), out var next, out int delta);
        Assert.Equal(-100, delta);
        Assert.Equal(0, next.Score); // clamped, not -70
    }

    [Fact]
    public void Recycle_DrawThree_ScoresMinusTwenty()
    {
        var state = State(options: GameOptions.DrawThree, waste: [Card(Suit.Hearts, 9)], score: 30);
        Klondike.TryApplyMove(state, Move.Recycle(), out var next, out int delta);
        Assert.Equal(-20, delta);
        Assert.Equal(10, next.Score);
    }

    [Fact]
    public void TableauToTableau_ScoresZero_WhenNoFlip()
    {
        var source = FaceUp(Card(Suit.Hearts, 8));
        var dest = FaceUp(Card(Suit.Spades, 9));
        var state = State(tableau: [source, dest], score: 50);
        Klondike.TryApplyMove(state, Move.TableauToTableau(0, 1, 1), out var next, out int delta);
        Assert.Equal(0, delta);
        Assert.Equal(50, next.Score);
    }

    [Fact]
    public void Draw_ScoresZero()
    {
        var state = State(stock: [Card(Suit.Clubs, 5)], score: 40);
        Klondike.TryApplyMove(state, Move.Draw(), out var next, out int delta);
        Assert.Equal(0, delta);
        Assert.Equal(40, next.Score);
    }
}
