using System.Collections.Immutable;

namespace Solitaire.Engine.Tests;

/// <summary>Helpers for constructing tailored <see cref="SpiderState"/> snapshots in tests.</summary>
internal static class SpiderTestStates
{
    public static Card Card(Suit suit, int rank) => new(suit, rank);

    public static TableauPile Pile(int faceDownCount, params Card[] cards) => new([.. cards], faceDownCount);

    public static TableauPile FaceUp(params Card[] cards) => Pile(0, cards);

    /// <summary>A face-up King→Ace run of one suit (a completable sequence), bottom-to-top.</summary>
    public static Card[] KingToAce(Suit suit)
    {
        var cards = new Card[13];
        for (int i = 0; i < 13; i++)
        {
            cards[i] = new Card(suit, 13 - i);
        }

        return cards;
    }

    public static SpiderState State(
        SpiderOptions? options = null,
        IEnumerable<Card>? stock = null,
        TableauPile[]? tableau = null,
        int completedSequences = 0,
        int score = SpiderScoring.InitialScore)
    {
        var piles = new TableauPile[SpiderState.TableauCount];
        for (int i = 0; i < piles.Length; i++)
        {
            piles[i] = tableau is not null && i < tableau.Length ? tableau[i] : TableauPile.Empty;
        }

        return new SpiderState(
            options ?? SpiderOptions.OneSuit,
            stock is null ? ImmutableArray<Card>.Empty : [.. stock],
            [.. piles],
            completedSequences,
            score);
    }
}
