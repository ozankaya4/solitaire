namespace Solitaire.Engine;

/// <summary>The kind of a <see cref="FreeCellMove"/>.</summary>
public enum FreeCellMoveType
{
    /// <summary>Move a run of cards from one tableau pile to another.</summary>
    TableauToTableau = 0,

    /// <summary>Move the top card of a tableau pile into an empty free cell.</summary>
    TableauToFreeCell = 1,

    /// <summary>Move the top card of a tableau pile onto its suit's foundation.</summary>
    TableauToFoundation = 2,

    /// <summary>Move a free cell's card onto a tableau pile.</summary>
    FreeCellToTableau = 3,

    /// <summary>Move a free cell's card onto its suit's foundation.</summary>
    FreeCellToFoundation = 4,

    /// <summary>Move the top card of a foundation back onto a tableau pile.</summary>
    FoundationToTableau = 5,
}

/// <summary>
/// A single player action. Immutable. Field meaning depends on <see cref="Type"/>;
/// use the static factory methods rather than the constructor.
/// </summary>
/// <param name="Type">The move kind.</param>
/// <param name="Source">
/// Source index: tableau pile 0..7 for tableau moves; free cell 0..3 for
/// free-cell moves; the <see cref="Suit"/> value (0..3) for
/// <see cref="FreeCellMoveType.FoundationToTableau"/>; otherwise -1.
/// </param>
/// <param name="Destination">
/// Destination index: tableau pile 0..7 for *-to-tableau moves; free cell 0..3
/// for <see cref="FreeCellMoveType.TableauToFreeCell"/>; otherwise -1.
/// </param>
/// <param name="Count">Number of cards moved (tableau-to-tableau); otherwise 1.</param>
public readonly record struct FreeCellMove(FreeCellMoveType Type, int Source, int Destination, int Count)
{
    /// <summary>Move <paramref name="count"/> cards from tableau <paramref name="from"/> to tableau <paramref name="to"/>.</summary>
    public static FreeCellMove TableauToTableau(int from, int to, int count) =>
        new(FreeCellMoveType.TableauToTableau, from, to, count);

    /// <summary>Move the top card of tableau <paramref name="from"/> into free cell <paramref name="cell"/>.</summary>
    public static FreeCellMove TableauToFreeCell(int from, int cell) =>
        new(FreeCellMoveType.TableauToFreeCell, from, cell, 1);

    /// <summary>Move the top card of tableau <paramref name="from"/> to its foundation.</summary>
    public static FreeCellMove TableauToFoundation(int from) =>
        new(FreeCellMoveType.TableauToFoundation, from, -1, 1);

    /// <summary>Move the card in free cell <paramref name="cell"/> onto tableau <paramref name="to"/>.</summary>
    public static FreeCellMove FreeCellToTableau(int cell, int to) =>
        new(FreeCellMoveType.FreeCellToTableau, cell, to, 1);

    /// <summary>Move the card in free cell <paramref name="cell"/> to its foundation.</summary>
    public static FreeCellMove FreeCellToFoundation(int cell) =>
        new(FreeCellMoveType.FreeCellToFoundation, cell, -1, 1);

    /// <summary>
    /// Move the top card of the <paramref name="suit"/> foundation to tableau
    /// <paramref name="to"/>.
    /// </summary>
    public static FreeCellMove FoundationToTableau(Suit suit, int to) =>
        new(FreeCellMoveType.FoundationToTableau, (int)suit, to, 1);
}
