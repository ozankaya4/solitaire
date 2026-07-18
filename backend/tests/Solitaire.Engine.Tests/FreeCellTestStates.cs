using System.Collections.Immutable;

namespace Solitaire.Engine.Tests;

/// <summary>Helpers for constructing tailored <see cref="FreeCellState"/> snapshots in tests.</summary>
internal static class FreeCellTestStates
{
    public static Card Card(Suit suit, int rank) => new(suit, rank);

    /// <summary>An all-face-up tableau pile (every FreeCell card is face-up).</summary>
    public static TableauPile Pile(params Card[] cards) => new([.. cards], 0);

    public static FreeCellState State(
        TableauPile[]? tableau = null,
        Card?[]? freeCells = null,
        int[]? foundations = null,
        int score = 0)
    {
        var piles = new TableauPile[FreeCellState.TableauCount];
        for (int i = 0; i < piles.Length; i++)
        {
            piles[i] = tableau is not null && i < tableau.Length ? tableau[i] : TableauPile.Empty;
        }

        var cells = new Card?[FreeCellState.FreeCellCount];
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = freeCells is not null && i < freeCells.Length ? freeCells[i] : null;
        }

        var foundationArray = foundations is null
            ? ImmutableArray.Create(0, 0, 0, 0)
            : [.. foundations];

        return new FreeCellState([.. piles], [.. cells], foundationArray, score);
    }
}
