using Xunit;
using static Solitaire.Engine.Tests.TriPeaksTestStates;

namespace Solitaire.Engine.Tests;

public class TriPeaksMoveLegalityTests
{
    // Children table (see TriPeaks.cs): 0->(3,4) 1->(5,6) 2->(7,8);
    // 3->(9,10) 4->(10,11) 5->(12,13) 6->(13,14) 7->(15,16) 8->(16,17);
    // 9->(18,19) 10->(19,20) 11->(20,21) 12->(21,22) 13->(22,23) 14->(23,24)
    // 15->(24,25) 16->(25,26) 17->(26,27); 18-27 = base row, always exposed.
    // Index 21 is shared: it is a child of both 11 (peak-0's last row) and 12
    // (peak-1's first row).

    [Fact]
    public void BaseRowCard_IsExposedImmediately()
    {
        var tableau = new Card?[28];
        tableau[20] = Card(Suit.Spades, 6);
        var state = State(tableau: tableau, waste: [Card(Suit.Hearts, 5)]); // 6 adjacent to 5

        bool ok = TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(20), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(10, delta);
        Assert.Null(next.Tableau[20]);
        Assert.Equal(Card(Suit.Spades, 6), next.WasteTop);
    }

    [Fact]
    public void NonBaseCard_IsNotExposed_WhileEitherChildRemains()
    {
        var tableau = new Card?[28];
        tableau[0] = Card(Suit.Spades, 5); // apex
        tableau[3] = Card(Suit.Hearts, 2); // child present
        tableau[4] = Card(Suit.Clubs, 3); // child present
        var blocked = State(tableau: tableau, waste: [Card(Suit.Diamonds, 6)]); // would be adjacent to 5
        Assert.False(TriPeaks.TryApplyMove(blocked, TriPeaksMove.PlayToWaste(0), out _, out _));

        var oneChildGone = (Card?[])tableau.Clone();
        oneChildGone[3] = null;
        var stillBlocked = State(tableau: oneChildGone, waste: [Card(Suit.Diamonds, 6)]);
        Assert.False(TriPeaks.TryApplyMove(stillBlocked, TriPeaksMove.PlayToWaste(0), out _, out _));
    }

    [Fact]
    public void NonBaseCard_BecomesExposed_OnceBothChildrenAreGone()
    {
        var tableau = new Card?[28];
        tableau[0] = Card(Suit.Spades, 5); // children (3,4) already null
        var state = State(tableau: tableau, waste: [Card(Suit.Diamonds, 6)]);

        bool ok = TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(0), out var next, out _);
        Assert.True(ok);
        Assert.Null(next.Tableau[0]);
    }

    [Fact]
    public void SharedBaseCell_ExposesBothAdjacentPeaksIndependently()
    {
        // Index 21 is the shared base cell between peak 0's index 11 and peak
        // 1's index 12. Clearing it contributes toward exposing each independently.
        var tableau = new Card?[28];
        tableau[11] = Card(Suit.Spades, 5);
        tableau[12] = Card(Suit.Hearts, 5);
        tableau[20] = Card(Suit.Clubs, 2); // index 11's other child
        tableau[22] = Card(Suit.Diamonds, 2); // index 12's other child
        // index 21 (shared) stays null: already "removed" for this snapshot.
        var state = State(tableau: tableau, waste: [Card(Suit.Spades, 4)]); // adjacent to a 5

        // Neither is exposed yet — each still has one live child (20 / 22).
        Assert.False(TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(11), out _, out _));
        Assert.False(TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(12), out _, out _));

        var clearIndex20 = (Card?[])tableau.Clone();
        clearIndex20[20] = null;
        var expose11 = State(tableau: clearIndex20, waste: [Card(Suit.Spades, 4)]);
        Assert.True(TriPeaks.TryApplyMove(expose11, TriPeaksMove.PlayToWaste(11), out _, out _));

        var clearIndex22 = (Card?[])tableau.Clone();
        clearIndex22[22] = null;
        var expose12 = State(tableau: clearIndex22, waste: [Card(Suit.Spades, 4)]);
        Assert.True(TriPeaks.TryApplyMove(expose12, TriPeaksMove.PlayToWaste(12), out _, out _));
    }

    [Theory]
    [InlineData(5, 6, true)] // one step up
    [InlineData(6, 5, true)] // one step down
    [InlineData(5, 7, false)] // two steps apart
    [InlineData(13, 1, true)] // King -> Ace wraparound
    [InlineData(1, 13, true)] // Ace -> King wraparound
    [InlineData(5, 5, false)] // same rank never matches
    public void PlayToWaste_RequiresRankAdjacency_WithKingAceWraparound(int tableauRank, int wasteRank, bool expectLegal)
    {
        var tableau = new Card?[28];
        tableau[20] = Card(Suit.Spades, tableauRank); // a base slot, always exposed
        var state = State(tableau: tableau, waste: [Card(Suit.Hearts, wasteRank)]);

        Assert.Equal(expectLegal, TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(20), out _, out _));
    }

    [Fact]
    public void PlayToWaste_WithEmptyWaste_IsIllegal()
    {
        var tableau = new Card?[28];
        tableau[20] = Card(Suit.Spades, 7);
        var state = State(tableau: tableau); // no waste yet
        Assert.False(TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(20), out _, out _));
    }

    [Fact]
    public void PlayToWaste_OnEmptySlot_IsIllegal()
    {
        var state = State(tableau: new Card?[28], waste: [Card(Suit.Hearts, 6)]);
        Assert.False(TriPeaks.TryApplyMove(state, TriPeaksMove.PlayToWaste(20), out _, out _));
    }

    [Fact]
    public void Draw_MovesStockTopToWaste()
    {
        var state = State(stock: [Card(Suit.Spades, 4), Card(Suit.Spades, 5)], waste: [Card(Suit.Hearts, 9)]);
        bool ok = TriPeaks.TryApplyMove(state, TriPeaksMove.Draw(), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(0, delta);
        Assert.Equal(Card(Suit.Spades, 4), next.WasteTop);
        Assert.Single(next.Stock);
    }

    [Fact]
    public void Recycle_RequiresEmptyStock_AndIsUnlimited()
    {
        var state = State(waste: [Card(Suit.Spades, 4), Card(Suit.Spades, 5)]);
        bool ok = TriPeaks.TryApplyMove(state, TriPeaksMove.Recycle(), out var next, out _);
        Assert.True(ok);
        Assert.Empty(next.Waste);
        Assert.Equal(2, next.Stock.Length);
        Assert.Equal(Card(Suit.Spades, 4), next.Stock[0]);

        // Recycling again immediately (after re-emptying the stock) is still legal —
        // there is no redeal counter/limit for TriPeaks.
        var drawnOnce = TriPeaks.TryApplyMove(next, TriPeaksMove.Draw(), out var afterDraw, out _);
        Assert.True(drawnOnce);
        var drawnTwice = TriPeaks.TryApplyMove(afterDraw, TriPeaksMove.Draw(), out var afterDraw2, out _);
        Assert.True(drawnTwice);
        Assert.True(TriPeaks.TryApplyMove(afterDraw2, TriPeaksMove.Recycle(), out _, out _));
    }

    [Fact]
    public void Recycle_WithNonEmptyStock_IsIllegal()
    {
        var state = State(stock: [Card(Suit.Spades, 4)], waste: [Card(Suit.Spades, 5)]);
        Assert.False(TriPeaks.TryApplyMove(state, TriPeaksMove.Recycle(), out _, out _));
    }

    [Fact]
    public void IsWon_OnlyWhenEveryTableauSlotIsCleared()
    {
        Assert.True(State(tableau: new Card?[28]).IsWon);

        var tableau = new Card?[28];
        tableau[20] = Card(Suit.Hearts, 2);
        Assert.False(State(tableau: tableau).IsWon);
    }

    [Fact]
    public void IsWon_IgnoresStockAndWaste()
    {
        var state = State(tableau: new Card?[28], stock: [Card(Suit.Spades, 2)], waste: [Card(Suit.Hearts, 9)]);
        Assert.True(state.IsWon);
    }

    [Fact]
    public void Replay_StopsAtFirstIllegalMove()
    {
        var moves = new List<TriPeaksMove> { TriPeaksMove.Draw(), TriPeaksMove.Recycle() }; // recycle w/ non-empty stock
        var result = TriPeaks.Replay(seed: 3, moves);
        Assert.False(result.AllMovesLegal);
        Assert.Equal(1, result.FirstIllegalMoveIndex);
        Assert.False(result.Won);
    }
}
