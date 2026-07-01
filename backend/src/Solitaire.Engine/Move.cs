namespace Solitaire.Engine;

/// <summary>The kind of a <see cref="Move"/>.</summary>
public enum MoveType
{
    /// <summary>Flip <see cref="GameOptions.DrawCount"/> cards from stock to waste.</summary>
    Draw = 0,

    /// <summary>Recycle the whole waste pile back into the stock (a redeal).</summary>
    Recycle = 1,

    /// <summary>Move the top waste card onto its suit's foundation.</summary>
    WasteToFoundation = 2,

    /// <summary>Move the top waste card onto a tableau pile.</summary>
    WasteToTableau = 3,

    /// <summary>Move a run of cards from one tableau pile to another.</summary>
    TableauToTableau = 4,

    /// <summary>Move the top card of a tableau pile onto its suit's foundation.</summary>
    TableauToFoundation = 5,

    /// <summary>Move the top card of a foundation back onto a tableau pile.</summary>
    FoundationToTableau = 6,
}

/// <summary>
/// A single player action. Immutable. Field meaning depends on <see cref="Type"/>;
/// use the static factory methods rather than the constructor. Unused fields are
/// set to -1 / 0 so the shape serializes cleanly for the shared test vectors.
/// </summary>
/// <param name="Type">The move kind.</param>
/// <param name="Source">
/// Source index: tableau pile 0..6 for tableau moves; the <see cref="Suit"/> value
/// (0..3) for <see cref="MoveType.FoundationToTableau"/>; otherwise -1.
/// </param>
/// <param name="Destination">Destination tableau pile 0..6, or -1 when not applicable.</param>
/// <param name="Count">Number of cards moved (tableau-to-tableau); otherwise 1 or 0.</param>
public readonly record struct Move(MoveType Type, int Source, int Destination, int Count)
{
    /// <summary>Flip cards from stock to waste.</summary>
    public static Move Draw() => new(MoveType.Draw, -1, -1, 0);

    /// <summary>Recycle the waste back into the stock.</summary>
    public static Move Recycle() => new(MoveType.Recycle, -1, -1, 0);

    /// <summary>Move the top waste card to its foundation.</summary>
    public static Move WasteToFoundation() => new(MoveType.WasteToFoundation, -1, -1, 1);

    /// <summary>Move the top waste card to tableau pile <paramref name="tableau"/>.</summary>
    public static Move WasteToTableau(int tableau) =>
        new(MoveType.WasteToTableau, -1, tableau, 1);

    /// <summary>
    /// Move <paramref name="count"/> cards from tableau <paramref name="from"/> to
    /// tableau <paramref name="to"/>.
    /// </summary>
    public static Move TableauToTableau(int from, int to, int count) =>
        new(MoveType.TableauToTableau, from, to, count);

    /// <summary>Move the top card of tableau <paramref name="from"/> to its foundation.</summary>
    public static Move TableauToFoundation(int from) =>
        new(MoveType.TableauToFoundation, from, -1, 1);

    /// <summary>
    /// Move the top card of the <paramref name="suit"/> foundation to tableau
    /// <paramref name="to"/>.
    /// </summary>
    public static Move FoundationToTableau(Suit suit, int to) =>
        new(MoveType.FoundationToTableau, (int)suit, to, 1);
}
