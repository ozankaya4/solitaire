namespace Solitaire.Engine;

/// <summary>
/// The minimal, variant-agnostic view of a game snapshot that the API needs for
/// replay and score verification. Every concrete state (Klondike, Spider, …)
/// implements it so callers can reason about score and completion uniformly.
/// </summary>
public interface IGameState
{
    /// <summary>Current running score (never negative).</summary>
    int Score { get; }

    /// <summary>True once the game is completely won.</summary>
    bool IsWon { get; }
}
