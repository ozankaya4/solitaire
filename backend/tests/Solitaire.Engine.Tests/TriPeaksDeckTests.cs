using Xunit;

namespace Solitaire.Engine.Tests;

public class TriPeaksDeckTests
{
    [Fact]
    public void NewGame_Deals28ToTableau_1ToWaste_23ToStock_NoDuplicates()
    {
        var state = TriPeaks.NewGame(seed: 1);

        Assert.Equal(28, state.Tableau.Length);
        Assert.All(state.Tableau, c => Assert.True(c.HasValue));
        Assert.Single(state.Waste);
        Assert.Equal(23, state.Stock.Length);

        var seen = new HashSet<int>();
        foreach (var card in state.Tableau)
        {
            Assert.True(seen.Add(card!.Value.OrdinalIndex));
        }
        foreach (var card in state.Stock)
        {
            Assert.True(seen.Add(card.OrdinalIndex));
        }
        foreach (var card in state.Waste)
        {
            Assert.True(seen.Add(card.OrdinalIndex));
        }
        Assert.Equal(52, seen.Count);
    }

    [Fact]
    public void NewGame_IsDeterministic_SameSeedSameDeal()
    {
        var a = TriPeaks.NewGame(seed: 42);
        var b = TriPeaks.NewGame(seed: 42);
        Assert.True(a.Tableau.SequenceEqual(b.Tableau));
        Assert.True(a.Stock.SequenceEqual(b.Stock));
        Assert.True(a.Waste.SequenceEqual(b.Waste));
    }

    [Fact]
    public void NewGame_DifferentSeeds_ProduceDifferentDeals()
    {
        var a = TriPeaks.NewGame(seed: 1);
        var b = TriPeaks.NewGame(seed: 2);
        Assert.False(a.Tableau.SequenceEqual(b.Tableau));
    }

    [Fact]
    public void NewGame_ScoreStartsAtZero_AndIsNotWon()
    {
        var state = TriPeaks.NewGame(seed: 1);
        Assert.Equal(0, state.Score);
        Assert.False(state.IsWon);
    }
}
