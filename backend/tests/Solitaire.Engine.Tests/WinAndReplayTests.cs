using System.Collections.Immutable;
using Xunit;
using static Solitaire.Engine.Tests.TestStates;

namespace Solitaire.Engine.Tests;

public class WinAndReplayTests
{
    [Fact]
    public void IsWon_TrueWhenAllFoundationsComplete()
    {
        var state = State(foundations: [13, 13, 13, 13]);
        Assert.True(state.IsWon);
        Assert.True(Klondike.IsWin(state));
    }

    [Fact]
    public void IsWon_FalseWhenAnyFoundationIncomplete()
    {
        var state = State(foundations: [13, 13, 13, 12]);
        Assert.False(state.IsWon);
    }

    [Fact]
    public void Replay_RejectsIllegalMoveAndReportsIndex()
    {
        // Legal draw, then an impossible waste->foundation (waste holds a non-Ace).
        var moves = new List<Move> { Move.Draw(), Move.WasteToFoundation(), Move.Draw() };
        var result = Klondike.Replay(1, GameOptions.DrawOne, moves);

        Assert.False(result.AllMovesLegal);
        Assert.False(result.Won);
        Assert.Equal(1, result.FirstIllegalMoveIndex);
    }

    [Fact]
    public void Replay_IsDeterministic()
    {
        var moves = new List<Move> { Move.Draw(), Move.Draw(), Move.Draw() };
        var a = Klondike.Replay(123, GameOptions.DrawThree, moves);
        var b = Klondike.Replay(123, GameOptions.DrawThree, moves);

        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.Won, b.Won);
        Assert.Equal(a.AllMovesLegal, b.AllMovesLegal);
        Assert.True(a.FinalState.Stock.SequenceEqual(b.FinalState.Stock));
        Assert.True(a.FinalState.Waste.SequenceEqual(b.FinalState.Waste));
    }

    [Fact]
    public void Replay_EmptyMoveList_ReturnsFreshGameNotWon()
    {
        var result = Klondike.Replay(5, GameOptions.DrawOne, []);
        Assert.True(result.AllMovesLegal);
        Assert.False(result.Won);
        Assert.Equal(0, result.Score);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Replay_OfSolvedGame_IsAValidWin(int seed)
    {
        var moves = KlondikeSolver.Solve(seed, GameOptions.DrawOne);
        Assert.NotNull(moves);

        var result = Klondike.Replay(seed, GameOptions.DrawOne, moves);
        Assert.True(result.AllMovesLegal);
        Assert.True(result.Won);
        Assert.True(result.FinalState.IsWon);
    }

    [Fact]
    public void Replay_DoesNotMutateInputMovesOrShareState()
    {
        // Building a fresh game twice and replaying the same moves must not let
        // one run affect another (snapshots are independent).
        var moves = new List<Move> { Move.Draw() };
        var first = Klondike.Replay(9, GameOptions.DrawOne, moves);
        var fresh = Klondike.NewGame(9, GameOptions.DrawOne);

        // The fresh game is untouched by the earlier replay.
        Assert.True(fresh.Waste.IsEmpty);
        Assert.Equal(24, fresh.Stock.Length);
        Assert.False(first.FinalState.Waste.IsEmpty);
    }

    [Fact]
    public void ApplyingMove_DoesNotMutateOriginalSnapshot()
    {
        var original = State(stock: [Card(Suit.Clubs, 5), Card(Suit.Hearts, 9)]);
        ImmutableArray<Card> stockBefore = original.Stock;

        Klondike.TryApplyMove(original, Move.Draw(), out _, out _);

        Assert.Equal(stockBefore, original.Stock); // unchanged
        Assert.Equal(2, original.Stock.Length);
        Assert.True(original.Waste.IsEmpty);
    }
}
