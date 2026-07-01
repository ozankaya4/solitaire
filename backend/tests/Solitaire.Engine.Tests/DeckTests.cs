using Xunit;

namespace Solitaire.Engine.Tests;

public class DeckTests
{
    [Fact]
    public void BuildOrdered_Has52DistinctCardsInCanonicalOrder()
    {
        var deck = Deck.BuildOrdered();

        Assert.Equal(52, deck.Length);
        Assert.Equal(52, deck.Distinct().Count());
        Assert.Equal(new Card(Suit.Clubs, 1), deck[0]);
        Assert.Equal(new Card(Suit.Spades, 13), deck[51]);

        for (int i = 0; i < 52; i++)
        {
            Assert.Equal(i, deck[i].OrdinalIndex);
        }
    }

    [Fact]
    public void Shuffle_IsDeterministicForASeed()
    {
        var a = Deck.Shuffle(2024);
        var b = Deck.Shuffle(2024);
        Assert.True(a.SequenceEqual(b));
    }

    [Fact]
    public void Shuffle_DiffersBetweenSeeds()
    {
        var a = Deck.Shuffle(1);
        var b = Deck.Shuffle(2);
        Assert.False(a.SequenceEqual(b));
    }

    [Fact]
    public void Shuffle_IsAPermutation_NoCardsLostOrDuplicated()
    {
        var shuffled = Deck.Shuffle(555);
        Assert.Equal(52, shuffled.Length);
        Assert.Equal(52, shuffled.Distinct().Count());
        Assert.Equal(Deck.BuildOrdered().OrderBy(c => c.OrdinalIndex), shuffled.OrderBy(c => c.OrdinalIndex));
    }

    [Fact]
    public void Deal_ProducesStandardKlondikeLayout()
    {
        var (tableau, stock) = Deck.Deal(Deck.Shuffle(2024));

        Assert.Equal(7, tableau.Length);
        for (int c = 0; c < 7; c++)
        {
            Assert.Equal(c + 1, tableau[c].Count);
            Assert.Equal(c, tableau[c].FaceDownCount); // one face-up card on top
            Assert.Equal(1, tableau[c].FaceUpCount);
        }

        Assert.Equal(24, stock.Length);
    }

    [Fact]
    public void Deal_UsesEveryCardExactlyOnce()
    {
        var (tableau, stock) = Deck.Deal(Deck.Shuffle(99));

        var all = tableau.SelectMany(p => p.Cards).Concat(stock).ToList();
        Assert.Equal(52, all.Count);
        Assert.Equal(52, all.Distinct().Count());
    }

    [Fact]
    public void NewGame_SameSeedAndOptions_ProducesIdenticalDeal()
    {
        var a = Klondike.NewGame(777, GameOptions.DrawThree);
        var b = Klondike.NewGame(777, GameOptions.DrawThree);

        Assert.True(a.Stock.SequenceEqual(b.Stock));
        Assert.True(a.Waste.SequenceEqual(b.Waste));
        for (int c = 0; c < 7; c++)
        {
            Assert.True(a.Tableau[c].Cards.SequenceEqual(b.Tableau[c].Cards));
            Assert.Equal(a.Tableau[c].FaceDownCount, b.Tableau[c].FaceDownCount);
        }
    }

    [Fact]
    public void NewGame_StartsWithZeroScoreAndEmptyFoundations()
    {
        var state = Klondike.NewGame(1, GameOptions.DrawOne);

        Assert.Equal(0, state.Score);
        Assert.Equal(0, state.RedealsUsed);
        Assert.True(state.Waste.IsEmpty);
        Assert.All(state.Foundations, top => Assert.Equal(0, top));
        Assert.False(state.IsWon);
    }
}
