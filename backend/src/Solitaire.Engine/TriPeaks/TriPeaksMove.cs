namespace Solitaire.Engine;

/// <summary>The kind of a <see cref="TriPeaksMove"/>.</summary>
public enum TriPeaksMoveType
{
    /// <summary>Flip one card from stock to waste.</summary>
    Draw = 0,

    /// <summary>Recycle the whole waste pile back into the stock (unlimited).</summary>
    Recycle = 1,

    /// <summary>Play an exposed tableau card onto the waste (rank-adjacent to the current top).</summary>
    PlayToWaste = 2,
}

/// <summary>
/// A single player action. Immutable. Field meaning depends on <see cref="Type"/>;
/// use the static factory methods rather than the constructor.
/// </summary>
/// <param name="Type">The move kind.</param>
/// <param name="Position">
/// The flat tableau index (0..27) of the card played, for
/// <see cref="TriPeaksMoveType.PlayToWaste"/>. Unused (-1) for Draw/Recycle.
/// </param>
public readonly record struct TriPeaksMove(TriPeaksMoveType Type, int Position)
{
    /// <summary>Flip a card from stock to waste.</summary>
    public static TriPeaksMove Draw() => new(TriPeaksMoveType.Draw, -1);

    /// <summary>Recycle the waste back into the stock.</summary>
    public static TriPeaksMove Recycle() => new(TriPeaksMoveType.Recycle, -1);

    /// <summary>Play the exposed tableau card at <paramref name="position"/> onto the waste.</summary>
    public static TriPeaksMove PlayToWaste(int position) => new(TriPeaksMoveType.PlayToWaste, position);
}
