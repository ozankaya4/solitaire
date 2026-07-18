using Xunit;
using static Solitaire.Engine.Tests.PyramidTestStates;

namespace Solitaire.Engine.Tests;

public class PyramidMoveLegalityTests
{
    // Flat indices: row0=0; row1=1,2; row2=3,4,5; row3=6..9; row4=10..14;
    // row5=15..20; row6(base)=21..27. Index 0's children are 1 and 2.

    [Fact]
    public void BaseRowCard_IsExposedImmediately_NothingElseNeeded()
    {
        var slots = EmptyPyramid();
        slots[27] = Card(Suit.Spades, 13); // last base-row slot
        var state = State(pyramid: slots);

        bool ok = Pyramid.TryApplyMove(state, PyramidMove.RemoveSingle(27), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(10, delta);
        Assert.Null(next.Pyramid[27]);
    }

    [Fact]
    public void NonBaseCard_IsNotExposed_WhileEitherChildRemains()
    {
        var slots = EmptyPyramid();
        slots[0] = Card(Suit.Spades, 13); // apex King
        slots[1] = Card(Suit.Hearts, 5); // child present
        slots[2] = Card(Suit.Clubs, 6); // child present
        var blocked = State(pyramid: slots);
        Assert.False(Pyramid.TryApplyMove(blocked, PyramidMove.RemoveSingle(0), out _, out _));

        // Only ONE child gone is still not enough.
        var oneChildGone = (Card?[])slots.Clone();
        oneChildGone[1] = null;
        var stillBlocked = State(pyramid: oneChildGone);
        Assert.False(Pyramid.TryApplyMove(stillBlocked, PyramidMove.RemoveSingle(0), out _, out _));
    }

    [Fact]
    public void NonBaseCard_BecomesExposed_OnceBothChildrenAreGone()
    {
        var slots = EmptyPyramid();
        slots[0] = Card(Suit.Spades, 13); // apex King; children (1,2) already null
        var state = State(pyramid: slots);

        bool ok = Pyramid.TryApplyMove(state, PyramidMove.RemoveSingle(0), out var next, out _);
        Assert.True(ok);
        Assert.Null(next.Pyramid[0]);
    }

    [Fact]
    public void RemoveSingle_NonKing_IsIllegal()
    {
        var slots = EmptyPyramid();
        slots[27] = Card(Suit.Hearts, 12); // Queen, not a King
        var state = State(pyramid: slots);
        Assert.False(Pyramid.TryApplyMove(state, PyramidMove.RemoveSingle(27), out _, out _));
    }

    [Fact]
    public void RemovePair_SummingToThirteen_IsLegal_AndScoresFifteen()
    {
        var slots = EmptyPyramid();
        slots[26] = Card(Suit.Hearts, 9);
        slots[27] = Card(Suit.Clubs, 4); // 9 + 4 = 13
        var state = State(pyramid: slots);

        bool ok = Pyramid.TryApplyMove(state, PyramidMove.RemovePair(26, 27), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(15, delta);
        Assert.Null(next.Pyramid[26]);
        Assert.Null(next.Pyramid[27]);
    }

    [Fact]
    public void RemovePair_NotSummingToThirteen_IsIllegal()
    {
        var slots = EmptyPyramid();
        slots[26] = Card(Suit.Hearts, 9);
        slots[27] = Card(Suit.Clubs, 5); // 9 + 5 = 14
        var state = State(pyramid: slots);
        Assert.False(Pyramid.TryApplyMove(state, PyramidMove.RemovePair(26, 27), out _, out _));
    }

    [Fact]
    public void RemovePair_WithWasteTop_IsLegal()
    {
        var slots = EmptyPyramid();
        slots[27] = Card(Suit.Hearts, 6);
        var state = State(pyramid: slots, waste: [Card(Suit.Clubs, 7)]); // 6 + 7 = 13

        bool ok = Pyramid.TryApplyMove(
            state, PyramidMove.RemovePair(27, PyramidMove.Waste), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(15, delta);
        Assert.Null(next.Pyramid[27]);
        Assert.Empty(next.Waste);
    }

    [Fact]
    public void RemoveSingle_FromWasteTop_IsLegal()
    {
        var state = State(waste: [Card(Suit.Diamonds, 13)]);
        bool ok = Pyramid.TryApplyMove(state, PyramidMove.RemoveSingle(PyramidMove.Waste), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(10, delta);
        Assert.Empty(next.Waste);
    }

    [Fact]
    public void RemovePair_CannotPairWasteWithItself()
    {
        var state = State(waste: [Card(Suit.Diamonds, 6)]);
        Assert.False(
            Pyramid.TryApplyMove(state, PyramidMove.RemovePair(PyramidMove.Waste, PyramidMove.Waste), out _, out _));
    }

    [Fact]
    public void Draw_MovesStockTopToWaste()
    {
        var state = State(stock: [Card(Suit.Spades, 4), Card(Suit.Spades, 5)]);
        bool ok = Pyramid.TryApplyMove(state, PyramidMove.Draw(), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(0, delta);
        Assert.Equal(Card(Suit.Spades, 4), next.WasteTop);
        Assert.Single(next.Stock);
    }

    [Fact]
    public void Recycle_RequiresEmptyStock_AndIsUnlimited()
    {
        var state = State(waste: [Card(Suit.Spades, 4), Card(Suit.Spades, 5)]);
        // waste[0] (first drawn) becomes the new stock top after recycling.
        bool ok = Pyramid.TryApplyMove(state, PyramidMove.Recycle(), out var next, out _);
        Assert.True(ok);
        Assert.Empty(next.Waste);
        Assert.Equal(2, next.Stock.Length);
        Assert.Equal(Card(Suit.Spades, 4), next.Stock[0]);

        // Recycling again immediately (after re-emptying the stock) is still legal —
        // there is no redeal counter/limit for Pyramid.
        var drawnOnce = Pyramid.TryApplyMove(next, PyramidMove.Draw(), out var afterDraw, out _);
        Assert.True(drawnOnce);
        var drawnTwice = Pyramid.TryApplyMove(afterDraw, PyramidMove.Draw(), out var afterDraw2, out _);
        Assert.True(drawnTwice);
        Assert.True(Pyramid.TryApplyMove(afterDraw2, PyramidMove.Recycle(), out _, out _));
    }

    [Fact]
    public void Recycle_WithNonEmptyStock_IsIllegal()
    {
        var state = State(stock: [Card(Suit.Spades, 4)], waste: [Card(Suit.Spades, 5)]);
        Assert.False(Pyramid.TryApplyMove(state, PyramidMove.Recycle(), out _, out _));
    }

    [Fact]
    public void IsWon_OnlyWhenEveryPyramidSlotIsCleared()
    {
        Assert.True(State(pyramid: EmptyPyramid()).IsWon);

        var slots = EmptyPyramid();
        slots[27] = Card(Suit.Hearts, 2);
        Assert.False(State(pyramid: slots).IsWon);
    }

    [Fact]
    public void IsWon_IgnoresStockAndWaste()
    {
        // Winning only requires the triangle to be clear — leftover stock/waste
        // cards are fine (per the project's chosen win condition).
        var state = State(pyramid: EmptyPyramid(), stock: [Card(Suit.Spades, 2)], waste: [Card(Suit.Hearts, 9)]);
        Assert.True(state.IsWon);
    }

    [Fact]
    public void Replay_StopsAtFirstIllegalMove()
    {
        var moves = new List<PyramidMove> { PyramidMove.Draw(), PyramidMove.Recycle() }; // recycle w/ non-empty stock
        var result = Pyramid.Replay(seed: 3, moves);
        Assert.False(result.AllMovesLegal);
        Assert.Equal(1, result.FirstIllegalMoveIndex);
        Assert.False(result.Won);
    }
}
