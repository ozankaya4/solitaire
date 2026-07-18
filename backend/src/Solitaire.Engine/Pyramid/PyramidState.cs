using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// An immutable snapshot of a Pyramid game. Every mutation produces a new
/// <see cref="PyramidState"/>, so replay and move validation are side-effect free.
/// </summary>
/// <remarks>
/// <para><see cref="Pyramid"/> is a flat, row-major array of length 28: row
/// <c>r</c> (0..6, 0 = the single top card, 6 = the 7-card base) occupies flat
/// indices <c>[r*(r+1)/2, r*(r+1)/2 + r]</c>. A removed card becomes null; the
/// slot is never reused. Card <c>(r, c)</c> rests on — and is covered by —
/// cards <c>(r+1, c)</c> and <c>(r+1, c+1)</c> in the row below; it is exposed
/// once both are gone (or immediately, for the base row).</para>
/// <para><see cref="Stock"/> — index 0 is the top (the next card drawn).</para>
/// <para><see cref="Waste"/> — the last element is the top (the only waste card
/// available to pair against).</para>
/// </remarks>
public sealed class PyramidState : IGameState
{
    /// <summary>Rows in the triangle (0..6).</summary>
    public const int RowCount = 7;

    /// <summary>Total cards in the triangle (1+2+...+7).</summary>
    public const int PyramidSize = 28;

    public PyramidState(
        ImmutableArray<Card?> pyramid,
        ImmutableArray<Card> stock,
        ImmutableArray<Card> waste,
        int score)
    {
        if (pyramid.Length != PyramidSize)
        {
            throw new ArgumentException("Pyramid must have length 28.", nameof(pyramid));
        }

        Pyramid = pyramid;
        Stock = stock;
        Waste = waste;
        Score = score;
    }

    /// <summary>The 28-card triangle, flat row-major, null = removed.</summary>
    public ImmutableArray<Card?> Pyramid { get; }

    /// <summary>Face-down draw pile; index 0 is the top (next drawn).</summary>
    public ImmutableArray<Card> Stock { get; }

    /// <summary>Face-up discard pile; the last element is the playable top card.</summary>
    public ImmutableArray<Card> Waste { get; }

    /// <summary>Current running score.</summary>
    public int Score { get; }

    /// <summary>The playable top waste card, or null if the waste is empty.</summary>
    public Card? WasteTop => Waste.IsEmpty ? null : Waste[^1];

    /// <summary>
    /// True once the triangle is fully cleared. The stock/waste need not be
    /// empty — clearing the 28-card pyramid is the whole win condition.
    /// </summary>
    public bool IsWon
    {
        get
        {
            foreach (var card in Pyramid)
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
    public PyramidState With(
        ImmutableArray<Card?>? pyramid = null,
        ImmutableArray<Card>? stock = null,
        ImmutableArray<Card>? waste = null,
        int? score = null) =>
        new(
            pyramid ?? Pyramid,
            stock ?? Stock,
            waste ?? Waste,
            score ?? Score);
}
