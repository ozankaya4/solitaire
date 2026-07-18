using Xunit;

namespace Solitaire.Engine.Tests;

public class FreeCellDeckTests
{
    [Fact]
    public void NewGame_Deals52Cards_AllFaceUp_NoDuplicates()
    {
        var state = FreeCell.NewGame(seed: 1);

        Assert.Equal(8, state.Tableau.Length);
        int total = 0;
        var seen = new HashSet<int>();
        foreach (var pile in state.Tableau)
        {
            Assert.Equal(0, pile.FaceDownCount); // every card is face-up
            total += pile.Count;
            foreach (var card in pile.Cards)
            {
                Assert.True(seen.Add(card.OrdinalIndex), "Duplicate card in deal.");
            }
        }

        Assert.Equal(52, total);
        Assert.Equal(52, seen.Count);
    }

    [Fact]
    public void NewGame_ColumnSizes_AreFourSevensThenFourSixes()
    {
        var state = FreeCell.NewGame(seed: 1);
        int[] sizes = [.. state.Tableau.Select(p => p.Count)];
        Assert.Equal([7, 7, 7, 7, 6, 6, 6, 6], sizes);
    }

    [Fact]
    public void NewGame_FreeCellsAndFoundationsStartEmpty()
    {
        var state = FreeCell.NewGame(seed: 1);
        Assert.All(state.FreeCells, c => Assert.Null(c));
        Assert.All(state.Foundations, f => Assert.Equal(0, f));
        Assert.Equal(0, state.Score);
        Assert.False(state.IsWon);
    }

    [Fact]
    public void NewGame_IsDeterministic_SameSeedSameDeal()
    {
        var a = FreeCell.NewGame(seed: 42);
        var b = FreeCell.NewGame(seed: 42);
        for (int i = 0; i < FreeCellState.TableauCount; i++)
        {
            // ImmutableArray<T>.Equals compares array identity, not contents —
            // SequenceEqual is the correct element-wise comparison here.
            Assert.True(a.Tableau[i].Cards.SequenceEqual(b.Tableau[i].Cards));
        }
    }

    [Fact]
    public void NewGame_DifferentSeeds_ProduceDifferentDeals()
    {
        var a = FreeCell.NewGame(seed: 1);
        var b = FreeCell.NewGame(seed: 2);
        Assert.False(a.Tableau[0].Cards.SequenceEqual(b.Tableau[0].Cards));
    }
}
