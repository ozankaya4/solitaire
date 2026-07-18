using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// An immutable snapshot of a TriPeaks game. Every mutation produces a new
/// <see cref="TriPeaksState"/>, so replay and move validation are side-effect free.
/// </summary>
/// <remarks>
/// <para><see cref="Tableau"/> is a flat array of length 28: three peaks sharing
/// a 10-card base row. Indices 0-2 are the peak apexes, 3-8 the next row (2 per
/// peak), 9-17 the row below that (3 per peak), 18-27 the shared base row (10
/// cards, always exposed). Each non-base card rests on — and is covered by —
/// exactly two cards in the row below it (see <see cref="TriPeaks"/>'s static
/// children table); it is exposed once both are gone.</para>
/// <para><see cref="Stock"/> — index 0 is the top (the next card drawn).</para>
/// <para><see cref="Waste"/> — the last element is the current card a tableau
/// card must be rank-adjacent to (King/Ace wrap around) in order to be played.</para>
/// </remarks>
public sealed class TriPeaksState : IGameState
{
    /// <summary>Total cards across the three peaks and their shared base row.</summary>
    public const int TableauSize = 28;

    public TriPeaksState(
        ImmutableArray<Card?> tableau,
        ImmutableArray<Card> stock,
        ImmutableArray<Card> waste,
        int score)
    {
        if (tableau.Length != TableauSize)
        {
            throw new ArgumentException("Tableau must have length 28.", nameof(tableau));
        }

        Tableau = tableau;
        Stock = stock;
        Waste = waste;
        Score = score;
    }

    /// <summary>The 28-card tableau (three peaks + shared base row), null = removed.</summary>
    public ImmutableArray<Card?> Tableau { get; }

    /// <summary>Face-down draw pile; index 0 is the top (next drawn).</summary>
    public ImmutableArray<Card> Stock { get; }

    /// <summary>Face-up discard pile; the last element is the card to build on.</summary>
    public ImmutableArray<Card> Waste { get; }

    /// <summary>Current running score.</summary>
    public int Score { get; }

    /// <summary>The current waste-top card to match against, or null if the waste is empty.</summary>
    public Card? WasteTop => Waste.IsEmpty ? null : Waste[^1];

    /// <summary>
    /// True once the tableau is fully cleared. The stock/waste need not be
    /// empty — clearing the three peaks is the whole win condition.
    /// </summary>
    public bool IsWon
    {
        get
        {
            foreach (var card in Tableau)
            {
                if (card is not null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Returns a copy with the supplied fields replaced.</summary>
    public TriPeaksState With(
        ImmutableArray<Card?>? tableau = null,
        ImmutableArray<Card>? stock = null,
        ImmutableArray<Card>? waste = null,
        int? score = null) =>
        new(
            tableau ?? Tableau,
            stock ?? Stock,
            waste ?? Waste,
            score ?? Score);
}
