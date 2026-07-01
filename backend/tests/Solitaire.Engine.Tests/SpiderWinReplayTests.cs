using Xunit;
using static Solitaire.Engine.Tests.SpiderTestStates;

namespace Solitaire.Engine.Tests;

public class SpiderWinReplayTests
{
    [Fact]
    public void IsWon_TrueWhenEightSequencesComplete()
    {
        var state = State(completedSequences: 8);
        Assert.True(state.IsWon);
        Assert.True(Spider.IsWin(state));
    }

    [Fact]
    public void IsWon_FalseWhenFewerThanEight()
    {
        Assert.False(State(completedSequences: 7).IsWon);
    }

    [Fact]
    public void CompletingTheFinalSequence_WinsTheGame()
    {
        // Seven sequences already done; complete the eighth by playing an Ace.
        var dest = FaceUp(KingToAce(Suit.Spades)[..12]);
        var source = FaceUp(Card(Suit.Spades, 1));
        var state = State(tableau: [source, dest], completedSequences: 7);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out _));
        Assert.Equal(8, next.CompletedSequences);
        Assert.True(next.IsWon);
    }

    [Fact]
    public void Replay_RejectsIllegalMoveAndReportsIndex()
    {
        // First deal is legal; an out-of-range tableau move is not.
        var moves = new List<SpiderMove>
        {
            SpiderMove.Deal(),
            SpiderMove.TableauToTableau(0, 0, 1), // same source/dest -> illegal
        };
        var result = Spider.Replay(1, SpiderOptions.OneSuit, moves);

        Assert.False(result.AllMovesLegal);
        Assert.False(result.Won);
        Assert.Equal(1, result.FirstIllegalMoveIndex);
    }

    [Fact]
    public void Replay_IsDeterministic()
    {
        var moves = new List<SpiderMove> { SpiderMove.Deal(), SpiderMove.Deal() };
        var a = Spider.Replay(123, SpiderOptions.FourSuit, moves);
        var b = Spider.Replay(123, SpiderOptions.FourSuit, moves);

        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.Won, b.Won);
        Assert.True(a.FinalState.Stock.SequenceEqual(b.FinalState.Stock));
    }

    [Fact]
    public void Replay_EmptyMoveList_ReturnsFreshGameNotWon()
    {
        var result = Spider.Replay(5, SpiderOptions.OneSuit, []);
        Assert.True(result.AllMovesLegal);
        Assert.False(result.Won);
        Assert.Equal(500, result.Score);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Replay_OfGreedyPlaythrough_IsAllLegal(int suitCount)
    {
        var options = new SpiderOptions(suitCount);
        var moves = SpiderSolver.GreedyPlaythrough(3, options, 200);
        var result = Spider.Replay(3, options, moves);

        Assert.True(result.AllMovesLegal);
        Assert.NotEmpty(moves);
    }

    [Fact]
    public void ApplyingMove_DoesNotMutateOriginalSnapshot()
    {
        var piles = new TableauPile[10];
        for (int i = 0; i < 10; i++)
        {
            piles[i] = FaceUp(Card(Suit.Spades, 5));
        }

        var original = State(tableau: piles, stock: Enumerable.Repeat(Card(Suit.Spades, 6), 10));
        int stockBefore = original.Stock.Length;

        Spider.TryApplyMove(original, SpiderMove.Deal(), out _, out _);

        Assert.Equal(stockBefore, original.Stock.Length); // unchanged
        Assert.Equal(1, original.Tableau[0].Count);
    }
}
