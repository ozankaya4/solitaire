using Xunit;
using static Solitaire.Engine.Tests.FreeCellTestStates;

namespace Solitaire.Engine.Tests;

public class FreeCellMoveLegalityTests
{
    // ---- Tableau -> tableau -------------------------------------------------

    [Fact]
    public void TableauToTableau_AnyCard_OnEmptyColumn_IsLegal()
    {
        // Unlike Klondike, FreeCell allows ANY rank (not just King) on an empty column.
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5)), TableauPile.Empty]);
        bool ok = FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 1), out var next, out _);
        Assert.True(ok);
        Assert.Equal(5, next.Tableau[1].TopCard!.Value.Rank);
    }

    [Fact]
    public void TableauToTableau_DescendingAlternatingColor_IsLegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5)), Pile(Card(Suit.Spades, 6))]);
        Assert.True(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 1), out _, out _));
    }

    [Fact]
    public void TableauToTableau_SameColor_IsIllegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5)), Pile(Card(Suit.Diamonds, 6))]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 1), out _, out _));
    }

    [Fact]
    public void TableauToTableau_WrongRank_IsIllegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5)), Pile(Card(Suit.Spades, 8))]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 1), out _, out _));
    }

    // Non-empty single-card filler for columns not otherwise involved in a
    // supermove test, so they don't silently count as "other empty columns".
    private static TableauPile Filler() => Pile(Card(Suit.Clubs, 10));

    [Fact]
    public void TableauToTableau_MultiCardRun_MovesTogether_WhenWithinSupermoveLimit()
    {
        // 3-card valid run, 2 free cells empty, no other empty columns: max movable
        // = (1 + 2) * 2^0 = 3. Exactly fits.
        var run = Pile(Card(Suit.Spades, 8), Card(Suit.Hearts, 7), Card(Suit.Clubs, 6));
        var state = State(
            tableau:
            [
                run, Pile(Card(Suit.Diamonds, 9)),
                Filler(), Filler(), Filler(), Filler(), Filler(), Filler(),
            ],
            freeCells: [Card(Suit.Hearts, 1), Card(Suit.Clubs, 2), null, null]); // 2 occupied, 2 free
        bool ok = FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 3), out var next, out _);
        Assert.True(ok);
        Assert.Equal(4, next.Tableau[1].Count);
        Assert.True(next.Tableau[0].IsEmpty);
    }

    [Fact]
    public void TableauToTableau_ExceedsSupermoveLimit_IsIllegal()
    {
        // Same 3-card run, but all 4 free cells occupied and no empty columns:
        // max movable = (1 + 0) * 2^0 = 1. Moving all 3 at once is illegal —
        // the 3-card move's placement (its BOTTOM card, S8, onto D9) is itself
        // legal by rank/color; only the resource limit blocks it.
        var run = Pile(Card(Suit.Spades, 8), Card(Suit.Hearts, 7), Card(Suit.Clubs, 6));
        var full = Card(Suit.Diamonds, 2);
        var state = State(
            tableau:
            [
                run, Pile(Card(Suit.Diamonds, 9)), Pile(Card(Suit.Diamonds, 7)),
                Filler(), Filler(), Filler(), Filler(), Filler(),
            ],
            freeCells: [full, full, full, full]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 3), out _, out _));
        // But moving just 1 card — the run's own TOP card, C6 — onto a
        // placement that fits IT (D7) still works: the limit, not legality, was
        // what blocked the 3-card move above.
        Assert.True(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 2, 1), out _, out _));
    }

    [Fact]
    public void TableauToTableau_EmptyColumns_MultiplySupermoveLimit()
    {
        // All free cells occupied; 2 OTHER empty columns (not the destination)
        // double the limit twice: (1+0) * 2^2 = 4.
        var run = Pile(
            Card(Suit.Spades, 9), Card(Suit.Hearts, 8), Card(Suit.Clubs, 7), Card(Suit.Diamonds, 6));
        var full = Card(Suit.Clubs, 2);
        var state = State(
            tableau:
            [
                run, Pile(Card(Suit.Hearts, 10)), TableauPile.Empty, TableauPile.Empty,
                Filler(), Filler(), Filler(), Filler(),
            ],
            freeCells: [full, full, full, full]);
        Assert.True(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 4), out _, out _));
    }

    [Fact]
    public void TableauToTableau_DestinationEmptyColumn_DoesNotCountTowardItsOwnLimit()
    {
        // 1 free cell empty, destination is the only empty column: max movable =
        // (1+1) * 2^0 = 2 (the empty destination itself must NOT inflate its own limit).
        var run = Pile(Card(Suit.Spades, 8), Card(Suit.Hearts, 7), Card(Suit.Clubs, 6));
        var full = Card(Suit.Diamonds, 2);
        var state = State(
            tableau:
            [
                run, TableauPile.Empty,
                Filler(), Filler(), Filler(), Filler(), Filler(), Filler(),
            ],
            freeCells: [null, full, full, full]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 3), out _, out _));
        Assert.True(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 2), out _, out _));
    }

    [Fact]
    public void TableauToTableau_NonSequentialRun_IsIllegal()
    {
        // Top two cards don't form a valid alternating-descending run.
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5), Card(Suit.Spades, 9)), TableauPile.Empty]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 2), out _, out _));
    }

    // ---- Tableau <-> free cell -----------------------------------------------

    [Fact]
    public void TableauToFreeCell_IntoEmptyCell_IsLegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5))]);
        bool ok = FreeCell.TryApplyMove(state, FreeCellMove.TableauToFreeCell(0, 2), out var next, out _);
        Assert.True(ok);
        Assert.Equal(Card(Suit.Hearts, 5), next.FreeCells[2]);
        Assert.True(next.Tableau[0].IsEmpty);
    }

    [Fact]
    public void TableauToFreeCell_IntoOccupiedCell_IsIllegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 5))], freeCells: [Card(Suit.Clubs, 2)]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToFreeCell(0, 0), out _, out _));
    }

    [Fact]
    public void FreeCellToTableau_LegalPlacement_Works()
    {
        var state = State(tableau: [Pile(Card(Suit.Spades, 6))], freeCells: [Card(Suit.Hearts, 5)]);
        bool ok = FreeCell.TryApplyMove(state, FreeCellMove.FreeCellToTableau(0, 0), out var next, out _);
        Assert.True(ok);
        Assert.Null(next.FreeCells[0]);
        Assert.Equal(2, next.Tableau[0].Count);
    }

    [Fact]
    public void FreeCellToTableau_OntoEmptyColumn_AnyCardIsLegal()
    {
        var state = State(tableau: [TableauPile.Empty], freeCells: [Card(Suit.Hearts, 5)]);
        Assert.True(FreeCell.TryApplyMove(state, FreeCellMove.FreeCellToTableau(0, 0), out _, out _));
    }

    [Fact]
    public void FreeCellToTableau_FromEmptyCell_IsIllegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Spades, 6))]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.FreeCellToTableau(0, 0), out _, out _));
    }

    // ---- Foundations ---------------------------------------------------------

    [Fact]
    public void TableauToFoundation_Ace_IsLegal_OnEmptyFoundation()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 1))]);
        bool ok = FreeCell.TryApplyMove(state, FreeCellMove.TableauToFoundation(0), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(1, next.Foundations[(int)Suit.Hearts]);
        Assert.Equal(10, delta);
    }

    [Fact]
    public void TableauToFoundation_OutOfOrder_IsIllegal()
    {
        var state = State(tableau: [Pile(Card(Suit.Hearts, 3))], foundations: [0, 0, 0, 0]);
        Assert.False(FreeCell.TryApplyMove(state, FreeCellMove.TableauToFoundation(0), out _, out _));
    }

    [Fact]
    public void FreeCellToFoundation_Works()
    {
        var state = State(freeCells: [Card(Suit.Clubs, 1)]);
        bool ok = FreeCell.TryApplyMove(state, FreeCellMove.FreeCellToFoundation(0), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(1, next.Foundations[(int)Suit.Clubs]);
        Assert.Null(next.FreeCells[0]);
        Assert.Equal(10, delta);
    }

    [Fact]
    public void FoundationToTableau_Works_AndScoresMinusFifteen()
    {
        var state = State(foundations: [0, 0, 3, 0], tableau: [Pile(Card(Suit.Spades, 4))], score: 100);
        bool ok = FreeCell.TryApplyMove(
            state, FreeCellMove.FoundationToTableau(Suit.Hearts, 0), out var next, out int delta);
        Assert.True(ok);
        Assert.Equal(2, next.Foundations[(int)Suit.Hearts]);
        Assert.Equal(-15, delta);
        Assert.Equal(85, next.Score);
    }

    [Fact]
    public void FoundationToTableau_OntoEmptyColumn_AnyCardIsLegal()
    {
        var state = State(foundations: [0, 0, 3, 0], tableau: [TableauPile.Empty]);
        Assert.True(FreeCell.TryApplyMove(state, FreeCellMove.FoundationToTableau(Suit.Hearts, 0), out _, out _));
    }

    // ---- Win detection ---------------------------------------------------------

    [Fact]
    public void IsWon_AllFoundationsAtKing()
    {
        var state = State(foundations: [13, 13, 13, 13]);
        Assert.True(state.IsWon);
        Assert.True(FreeCell.IsWin(state));
    }

    [Fact]
    public void IsWon_False_WhenAnyFoundationIncomplete()
    {
        var state = State(foundations: [13, 13, 12, 13]);
        Assert.False(state.IsWon);
    }

    // ---- Replay / illegal-move reporting ---------------------------------------

    [Fact]
    public void Replay_StopsAtFirstIllegalMove()
    {
        var moves = new List<FreeCellMove>
        {
            FreeCellMove.TableauToFreeCell(0, 0),
            FreeCellMove.TableauToFreeCell(0, 0), // now-empty column has no top card
        };
        var result = FreeCell.Replay(seed: 5, moves);
        Assert.False(result.AllMovesLegal);
        Assert.Equal(1, result.FirstIllegalMoveIndex);
        Assert.False(result.Won);
    }
}
