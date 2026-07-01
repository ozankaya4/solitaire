namespace Solitaire.Engine;

/// <summary>
/// A portable, serialization-friendly representation of a move that is common to
/// all variants. Each engine converts its own strongly-typed move to/from this
/// shape, which is exactly what the shared JSON test vectors store and what the
/// API accepts when a client submits a game for verification.
/// </summary>
/// <param name="Type">The variant's move-type name (e.g. "Draw", "Deal", "TableauToTableau").</param>
/// <param name="Source">Source index (pile/suit), when applicable.</param>
/// <param name="Destination">Destination index, when applicable.</param>
/// <param name="Count">Number of cards moved, when applicable.</param>
public sealed record MoveDto(
    string Type,
    int? Source = null,
    int? Destination = null,
    int? Count = null);
