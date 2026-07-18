using System.Collections.Immutable;

namespace Solitaire.Engine.Tests;

/// <summary>Helpers for constructing tailored <see cref="TriPeaksState"/> snapshots in tests.</summary>
internal static class TriPeaksTestStates
{
    public static Card Card(Suit suit, int rank) => new(suit, rank);

    public static TriPeaksState State(
        Card?[]? tableau = null,
        IEnumerable<Card>? stock = null,
        IEnumerable<Card>? waste = null,
        int score = 0)
    {
        var slots = new Card?[TriPeaksState.TableauSize];
        if (tableau is not null)
        {
            for (int i = 0; i < tableau.Length && i < slots.Length; i++)
            {
                slots[i] = tableau[i];
            }
        }

        return new TriPeaksState(
            [.. slots],
            stock is null ? ImmutableArray<Card>.Empty : [.. stock],
            waste is null ? ImmutableArray<Card>.Empty : [.. waste],
            score);
    }
}
