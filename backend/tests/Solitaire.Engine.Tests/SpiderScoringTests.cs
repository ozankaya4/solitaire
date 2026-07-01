using Xunit;
using static Solitaire.Engine.Tests.SpiderTestStates;

namespace Solitaire.Engine.Tests;

public class SpiderScoringTests
{
    [Fact]
    public void NewGame_StartsAtFiveHundred() =>
        Assert.Equal(500, Spider.NewGame(1, SpiderOptions.OneSuit).Score);

    [Fact]
    public void TableauMove_CostsOnePoint()
    {
        var state = State(
            tableau: [FaceUp(Card(Suit.Spades, 8)), FaceUp(Card(Suit.Spades, 9))],
            score: 500);
        Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out int delta);
        Assert.Equal(-1, delta);
        Assert.Equal(499, next.Score);
    }

    [Fact]
    public void Deal_CostsOnePoint()
    {
        var piles = new TableauPile[10];
        for (int i = 0; i < 10; i++)
        {
            piles[i] = FaceUp(Card(Suit.Spades, 5));
        }

        var state = State(tableau: piles, stock: Enumerable.Repeat(Card(Suit.Spades, 6), 10), score: 500);
        Spider.TryApplyMove(state, SpiderMove.Deal(), out var next, out int delta);
        Assert.Equal(-1, delta);
        Assert.Equal(499, next.Score);
    }

    [Fact]
    public void CompletingSequence_Adds100MinusTheMoveCost()
    {
        var dest = FaceUp(KingToAce(Suit.Spades)[..12]);
        var source = FaceUp(Card(Suit.Spades, 1));
        var state = State(tableau: [source, dest], score: 300);

        Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out int delta);
        Assert.Equal(99, delta); // -1 + 100
        Assert.Equal(399, next.Score);
    }

    [Fact]
    public void Score_IsClampedAtZero()
    {
        var state = State(
            tableau: [FaceUp(Card(Suit.Spades, 8)), FaceUp(Card(Suit.Spades, 9))],
            score: 0);
        Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out _);
        Assert.Equal(0, next.Score); // 0 - 1 clamped to 0
    }
}
