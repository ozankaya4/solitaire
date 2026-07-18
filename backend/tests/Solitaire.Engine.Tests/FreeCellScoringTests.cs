using Xunit;
using static Solitaire.Engine.Tests.FreeCellTestStates;

namespace Solitaire.Engine.Tests;

public class FreeCellScoringTests
{
    [Fact]
    public void FoundationToTableau_Penalty_ClampsAtZero()
    {
        var state = State(foundations: [0, 0, 3, 0], tableau: [TableauPile.Empty], score: 10);
        FreeCell.TryApplyMove(state, FreeCellMove.FoundationToTableau(Suit.Hearts, 0), out var next, out int delta);
        Assert.Equal(-15, delta);
        Assert.Equal(0, next.Score); // clamped, not -5
    }

    [Fact]
    public void TableauToFreeCell_And_FreeCellToTableau_ScoreZero()
    {
        var toCell = State(tableau: [Pile(Card(Suit.Hearts, 5))], score: 20);
        FreeCell.TryApplyMove(toCell, FreeCellMove.TableauToFreeCell(0, 0), out var afterToCell, out int d1);
        Assert.Equal(0, d1);
        Assert.Equal(20, afterToCell.Score);

        var fromCell = State(tableau: [Pile(Card(Suit.Spades, 6))], freeCells: [Card(Suit.Hearts, 5)], score: 20);
        FreeCell.TryApplyMove(fromCell, FreeCellMove.FreeCellToTableau(0, 0), out var afterFromCell, out int d2);
        Assert.Equal(0, d2);
        Assert.Equal(20, afterFromCell.Score);
    }

    [Fact]
    public void TableauToTableau_ScoresZero_NoFlipConceptInFreeCell()
    {
        var state = State(
            tableau: [Pile(Card(Suit.Hearts, 8)), Pile(Card(Suit.Spades, 9))], score: 50);
        FreeCell.TryApplyMove(state, FreeCellMove.TableauToTableau(0, 1, 1), out var next, out int delta);
        Assert.Equal(0, delta);
        Assert.Equal(50, next.Score);
    }
}
