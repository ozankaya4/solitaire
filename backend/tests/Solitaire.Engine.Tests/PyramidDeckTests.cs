using Xunit;

namespace Solitaire.Engine.Tests;

public class PyramidDeckTests
{
    [Fact]
    public void NewGame_Deals28ToPyramid_24ToStock_NoDuplicates()
    {
        var state = Pyramid.NewGame(seed: 1);

        Assert.Equal(28, state.Pyramid.Length);
        Assert.All(state.Pyramid, c => Assert.True(c.HasValue));
        Assert.Equal(24, state.Stock.Length);
        Assert.Empty(state.Waste);

        var seen = new HashSet<int>();
        foreach (var card in state.Pyramid)
        {
            Assert.True(seen.Add(card!.Value.OrdinalIndex));
        }
        foreach (var card in state.Stock)
        {
            Assert.True(seen.Add(card.OrdinalIndex));
        }
        Assert.Equal(52, seen.Count);
    }

    [Fact]
    public void NewGame_IsDeterministic_SameSeedSameDeal()
    {
        var a = Pyramid.NewGame(seed: 42);
        var b = Pyramid.NewGame(seed: 42);
        Assert.True(a.Pyramid.SequenceEqual(b.Pyramid));
        Assert.True(a.Stock.SequenceEqual(b.Stock));
    }

    [Fact]
    public void NewGame_DifferentSeeds_ProduceDifferentDeals()
    {
        var a = Pyramid.NewGame(seed: 1);
        var b = Pyramid.NewGame(seed: 2);
        Assert.False(a.Pyramid.SequenceEqual(b.Pyramid));
    }

    [Fact]
    public void NewGame_ScoreStartsAtZero_AndIsNotWon()
    {
        var state = Pyramid.NewGame(seed: 1);
        Assert.Equal(0, state.Score);
        Assert.False(state.IsWon);
    }
}
