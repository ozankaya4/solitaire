using System.Collections.Immutable;

namespace Solitaire.Engine.Tests;

/// <summary>Helpers for constructing tailored <see cref="GameState"/> snapshots in tests.</summary>
internal static class TestStates
{
    public static Card Card(Suit suit, int rank) => new(suit, rank);

    /// <summary>Builds a tableau pile from bottom-to-top cards with a face-down count.</summary>
    public static TableauPile Pile(int faceDownCount, params Card[] cards) =>
        new([.. cards], faceDownCount);

    /// <summary>An all-face-up pile (nothing hidden).</summary>
    public static TableauPile FaceUp(params Card[] cards) => Pile(0, cards);

    public static GameState State(
        GameOptions? options = null,
        IEnumerable<Card>? stock = null,
        IEnumerable<Card>? waste = null,
        int[]? foundations = null,
        TableauPile[]? tableau = null,
        int score = 0,
        int redealsUsed = 0)
    {
        var piles = new TableauPile[GameState.TableauCount];
        for (int i = 0; i < piles.Length; i++)
        {
            piles[i] = tableau is not null && i < tableau.Length ? tableau[i] : TableauPile.Empty;
        }

        var foundationArray = foundations is null
            ? ImmutableArray.Create(0, 0, 0, 0)
            : [.. foundations];

        return new GameState(
            options ?? GameOptions.DrawOne,
            stock is null ? ImmutableArray<Card>.Empty : [.. stock],
            waste is null ? ImmutableArray<Card>.Empty : [.. waste],
            foundationArray,
            [.. piles],
            score,
            redealsUsed);
    }
}
