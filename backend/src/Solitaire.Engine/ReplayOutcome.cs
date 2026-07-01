namespace Solitaire.Engine;

/// <summary>
/// The variant-neutral result of <see cref="ISolitaireEngine.Replay"/>: everything
/// the API needs to verify a submitted game, without exposing a concrete state type.
/// </summary>
/// <param name="Score">Final (clamped, non-negative) score.</param>
/// <param name="Won">True only if every move was legal and the game is complete.</param>
/// <param name="AllMovesLegal">True if every move in the sequence was legal.</param>
/// <param name="FirstIllegalMoveIndex">Index of the first illegal move, or null.</param>
public readonly record struct ReplayOutcome(
    int Score,
    bool Won,
    bool AllMovesLegal,
    int? FirstIllegalMoveIndex);
