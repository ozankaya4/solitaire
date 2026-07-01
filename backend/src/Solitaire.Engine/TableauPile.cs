using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// One of the seven tableau columns. Immutable.
/// </summary>
/// <remarks>
/// Cards are stored bottom-to-top: <c>Cards[0]</c> is the bottom of the pile and
/// <c>Cards[^1]</c> is the exposed top. In Klondike the face-down cards are always
/// a contiguous block at the bottom, so a single <see cref="FaceDownCount"/> fully
/// describes which cards are hidden: indices <c>[0, FaceDownCount)</c> are face-down
/// and <c>[FaceDownCount, Count)</c> are face-up.
/// </remarks>
public sealed class TableauPile
{
    /// <summary>An empty pile.</summary>
    public static TableauPile Empty { get; } = new(ImmutableArray<Card>.Empty, 0);

    /// <summary>Cards from bottom (index 0) to top (last index).</summary>
    public ImmutableArray<Card> Cards { get; }

    /// <summary>Count of face-down cards at the bottom of the pile.</summary>
    public int FaceDownCount { get; }

    /// <summary>Total number of cards in the pile.</summary>
    public int Count => Cards.Length;

    /// <summary>True when the pile has no cards.</summary>
    public bool IsEmpty => Cards.IsEmpty;

    /// <summary>Number of face-up cards (the movable run at the top).</summary>
    public int FaceUpCount => Count - FaceDownCount;

    public TableauPile(ImmutableArray<Card> cards, int faceDownCount)
    {
        if (faceDownCount < 0 || faceDownCount > cards.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceDownCount), faceDownCount, "faceDownCount out of range for pile.");
        }

        Cards = cards;
        FaceDownCount = faceDownCount;
    }

    /// <summary>The exposed top card, or null if the pile is empty.</summary>
    public Card? TopCard => IsEmpty ? null : Cards[^1];

    /// <summary>True if the card at <paramref name="index"/> (from the bottom) is face-up.</summary>
    public bool IsFaceUp(int index) => index >= FaceDownCount;
}
