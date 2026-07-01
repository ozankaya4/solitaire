namespace Solitaire.Engine;

/// <summary>
/// The outcome of <see cref="Klondike.Replay"/>.
/// </summary>
/// <param name="FinalState">
/// The state after the last applied move. If a move was illegal this is the state
/// just before it (the illegal move is not applied).
/// </param>
/// <param name="Score">The final (clamped, non-negative) score.</param>
/// <param name="Won">True only if all moves were legal and the game is complete.</param>
/// <param name="AllMovesLegal">True if every move in the sequence was legal.</param>
/// <param name="FirstIllegalMoveIndex">
/// The zero-based index of the first illegal move, or null if all were legal.
/// </param>
public readonly record struct ReplayResult(
    GameState FinalState,
    int Score,
    bool Won,
    bool AllMovesLegal,
    int? FirstIllegalMoveIndex);
