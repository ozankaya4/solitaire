namespace Solitaire.Engine;

/// <summary>The kind of a <see cref="SpiderMove"/>.</summary>
public enum SpiderMoveType
{
    /// <summary>Deal one card face-up onto each of the ten tableau piles.</summary>
    Deal = 0,

    /// <summary>Move a same-suit descending run from one tableau pile to another.</summary>
    TableauToTableau = 1,
}

/// <summary>
/// A single Spider action. Immutable. Completing a K→A suit sequence is automatic
/// (it happens inside move application), so there is no explicit "complete" move.
/// </summary>
/// <param name="Type">The move kind.</param>
/// <param name="Source">Source tableau pile 0..9, or -1 for a deal.</param>
/// <param name="Destination">Destination tableau pile 0..9, or -1 for a deal.</param>
/// <param name="Count">Number of cards moved, or 0 for a deal.</param>
public readonly record struct SpiderMove(SpiderMoveType Type, int Source, int Destination, int Count)
{
    /// <summary>Deal a row from the stock.</summary>
    public static SpiderMove Deal() => new(SpiderMoveType.Deal, -1, -1, 0);

    /// <summary>Move <paramref name="count"/> cards from pile <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static SpiderMove TableauToTableau(int from, int to, int count) =>
        new(SpiderMoveType.TableauToTableau, from, to, count);
}
