namespace Solitaire.Engine;

/// <summary>The kind of a <see cref="PyramidMove"/>.</summary>
public enum PyramidMoveType
{
    /// <summary>Flip one card from stock to waste.</summary>
    Draw = 0,

    /// <summary>Recycle the whole waste pile back into the stock (unlimited).</summary>
    Recycle = 1,

    /// <summary>Remove a single exposed King (rank 13 needs no partner).</summary>
    RemoveSingle = 2,

    /// <summary>Remove two exposed cards whose ranks sum to 13.</summary>
    RemovePair = 3,
}

/// <summary>
/// A single player action. Immutable. Field meaning depends on <see cref="Type"/>;
/// use the static factory methods rather than the constructor.
/// </summary>
/// <param name="Type">The move kind.</param>
/// <param name="PositionA">
/// A position: <c>0..27</c> addresses a flat pyramid slot; <c>-1</c> means "the
/// waste's top card". Used by <see cref="PyramidMoveType.RemoveSingle"/> (the
/// card removed) and <see cref="PyramidMoveType.RemovePair"/> (the first card).
/// Unused (-1) for Draw/Recycle.
/// </param>
/// <param name="PositionB">
/// The second card for <see cref="PyramidMoveType.RemovePair"/>, using the same
/// position encoding as <paramref name="PositionA"/>. Unused (-1) otherwise.
/// </param>
public readonly record struct PyramidMove(PyramidMoveType Type, int PositionA, int PositionB)
{
    /// <summary>Sentinel position meaning "the waste's top card" rather than a pyramid slot.</summary>
    public const int Waste = -1;

    /// <summary>Flip a card from stock to waste.</summary>
    public static PyramidMove Draw() => new(PyramidMoveType.Draw, -1, -1);

    /// <summary>Recycle the waste back into the stock.</summary>
    public static PyramidMove Recycle() => new(PyramidMoveType.Recycle, -1, -1);

    /// <summary>Remove a single King at <paramref name="position"/> (pyramid slot or <see cref="Waste"/>).</summary>
    public static PyramidMove RemoveSingle(int position) => new(PyramidMoveType.RemoveSingle, position, -1);

    /// <summary>
    /// Remove the two cards at <paramref name="positionA"/> and
    /// <paramref name="positionB"/> (each a pyramid slot or <see cref="Waste"/>;
    /// at most one may be <see cref="Waste"/>).
    /// </summary>
    public static PyramidMove RemovePair(int positionA, int positionB) =>
        new(PyramidMoveType.RemovePair, positionA, positionB);
}
