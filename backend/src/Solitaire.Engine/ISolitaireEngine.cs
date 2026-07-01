namespace Solitaire.Engine;

/// <summary>
/// The common contract every solitaire variant exposes so the API can treat them
/// uniformly for replay and score verification. Implementations are stateless and
/// their <see cref="Replay"/> is a pure function of the <see cref="GameDefinition"/>.
/// </summary>
public interface ISolitaireEngine
{
    /// <summary>Stable, lowercase variant id, e.g. "klondike" or "spider".</summary>
    string Variant { get; }

    /// <summary>
    /// Deterministically replays a serialized game and returns the verified
    /// outcome. Never throws for a merely illegal move — an illegal move stops the
    /// replay and is reported via <see cref="ReplayOutcome.FirstIllegalMoveIndex"/>.
    /// Throws only for structurally invalid input (e.g. missing/invalid options).
    /// </summary>
    ReplayOutcome Replay(GameDefinition game);
}
