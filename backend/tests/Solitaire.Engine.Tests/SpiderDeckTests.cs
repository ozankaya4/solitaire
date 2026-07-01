using Xunit;

namespace Solitaire.Engine.Tests;

public class SpiderDeckTests
{
    [Theory]
    [InlineData(1, 1)] // 1 suit  -> 8 copies of each rank
    [InlineData(2, 2)] // 2 suits -> 4 copies
    [InlineData(4, 4)] // 4 suits -> 2 copies
    public void BuildOrdered_Has104CardsWithExpectedSuitCount(int suitCount, int expectedSuits)
    {
        var deck = SpiderDeck.BuildOrdered(suitCount);

        Assert.Equal(104, deck.Length);
        Assert.Equal(expectedSuits, deck.Select(c => c.Suit).Distinct().Count());
        Assert.All(deck, c => Assert.InRange(c.Rank, 1, 13));

        // Every (suit, rank) present the same number of times.
        int copies = 104 / (13 * suitCount);
        foreach (var group in deck.GroupBy(c => c))
        {
            Assert.Equal(copies, group.Count());
        }
    }

    [Fact]
    public void BuildOrdered_OneSuit_IsAllSpades()
    {
        var deck = SpiderDeck.BuildOrdered(1);
        Assert.All(deck, c => Assert.Equal(Suit.Spades, c.Suit));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void BuildOrdered_InvalidSuitCount_Throws(int suitCount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SpiderDeck.BuildOrdered(suitCount));

    [Fact]
    public void Shuffle_IsDeterministicForASeed()
    {
        var a = SpiderDeck.Shuffle(2024, 4);
        var b = SpiderDeck.Shuffle(2024, 4);
        Assert.True(a.SequenceEqual(b));
    }

    [Fact]
    public void Shuffle_DiffersBetweenSeeds()
    {
        var a = SpiderDeck.Shuffle(1, 4);
        var b = SpiderDeck.Shuffle(2, 4);
        Assert.False(a.SequenceEqual(b));
    }

    [Fact]
    public void Shuffle_PreservesTheMultiset()
    {
        var ordered = SpiderDeck.BuildOrdered(4);
        var shuffled = SpiderDeck.Shuffle(555, 4);
        Assert.Equal(
            ordered.OrderBy(c => c.OrdinalIndex),
            shuffled.OrderBy(c => c.OrdinalIndex));
    }

    [Fact]
    public void Deal_ProducesStandardSpiderLayout()
    {
        var (tableau, stock) = SpiderDeck.Deal(SpiderDeck.Shuffle(2024, 4));

        Assert.Equal(10, tableau.Length);
        for (int c = 0; c < 10; c++)
        {
            int expected = c < 4 ? 6 : 5;
            Assert.Equal(expected, tableau[c].Count);
            Assert.Equal(expected - 1, tableau[c].FaceDownCount); // one face-up card
        }

        Assert.Equal(50, stock.Length);
    }

    [Fact]
    public void Deal_UsesEveryCardExactlyOnce()
    {
        var shuffled = SpiderDeck.Shuffle(99, 2);
        var (tableau, stock) = SpiderDeck.Deal(shuffled);

        var all = tableau.SelectMany(p => p.Cards).Concat(stock).ToList();
        Assert.Equal(104, all.Count);
        Assert.Equal(
            shuffled.OrderBy(c => c.OrdinalIndex),
            all.OrderBy(c => c.OrdinalIndex));
    }

    [Fact]
    public void NewGame_SameSeedAndOptions_ProducesIdenticalDeal()
    {
        var a = Spider.NewGame(777, SpiderOptions.TwoSuit);
        var b = Spider.NewGame(777, SpiderOptions.TwoSuit);

        Assert.True(a.Stock.SequenceEqual(b.Stock));
        for (int c = 0; c < 10; c++)
        {
            Assert.True(a.Tableau[c].Cards.SequenceEqual(b.Tableau[c].Cards));
        }
    }

    [Fact]
    public void NewGame_StartsAtFiveHundredWithNothingCompleted()
    {
        var state = Spider.NewGame(1, SpiderOptions.OneSuit);
        Assert.Equal(SpiderScoring.InitialScore, state.Score);
        Assert.Equal(0, state.CompletedSequences);
        Assert.False(state.IsWon);
        Assert.Equal(50, state.Stock.Length);
    }
}
