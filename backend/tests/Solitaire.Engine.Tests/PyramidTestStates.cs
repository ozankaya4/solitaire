using System.Collections.Immutable;

namespace Solitaire.Engine.Tests;

/// <summary>Helpers for constructing tailored <see cref="PyramidState"/> snapshots in tests.</summary>
internal static class PyramidTestStates
{
    public static Card Card(Suit suit, int rank) => new(suit, rank);

    /// <summary>
    /// Builds a full 28-slot pyramid from a row-major sequence of nullable cards
    /// (pass fewer than 28 to fill the remainder with nulls — "already removed").
    /// </summary>
    public static ImmutableArray<Card?> Pyramid(params Card?[] cards)
    {
        var slots = new Card?[PyramidState.PyramidSize];
        for (int i = 0; i < cards.Length && i < slots.Length; i++)
        {
            slots[i] = cards[i];
        }

        return [.. slots];
    }

    /// <summary>An empty (fully-cleared) pyramid, useful as a base to override specific slots on.</summary>
    public static Card?[] EmptyPyramid() => new Card?[PyramidState.PyramidSize];

    public static PyramidState State(
        Card?[]? pyramid = null,
        IEnumerable<Card>? stock = null,
        IEnumerable<Card>? waste = null,
        int score = 0)
    {
        var slots = new Card?[PyramidState.PyramidSize];
        if (pyramid is not null)
        {
            for (int i = 0; i < pyramid.Length && i < slots.Length; i++)
            {
                slots[i] = pyramid[i];
            }
        }

        return new PyramidState(
            [.. slots],
            stock is null ? ImmutableArray<Card>.Empty : [.. stock],
            waste is null ? ImmutableArray<Card>.Empty : [.. waste],
            score);
    }
}
