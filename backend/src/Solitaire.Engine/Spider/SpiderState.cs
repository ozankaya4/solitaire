using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// An immutable snapshot of a Spider game. Like <see cref="GameState"/> for
/// Klondike, every mutation yields a new instance so replay is side-effect free.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Stock"/> — face-down cards dealt a row (10) at a time; index 0
/// is the next card dealt.</item>
/// <item><see cref="Tableau"/> — the ten columns (see <see cref="TableauPile"/>).</item>
/// <item><see cref="CompletedSequences"/> — number of K→A suit runs removed to the
/// foundation (0..8). Eight means the game is won.</item>
/// </list>
/// </remarks>
public sealed class SpiderState : IGameState
{
    /// <summary>Number of tableau columns in Spider.</summary>
    public const int TableauCount = 10;

    /// <summary>Total suit sequences to complete in order to win.</summary>
    public const int TotalSequences = 8;

    public SpiderState(
        SpiderOptions options,
        ImmutableArray<Card> stock,
        ImmutableArray<TableauPile> tableau,
        int completedSequences,
        int score)
    {
        if (tableau.Length != TableauCount)
        {
            throw new ArgumentException("Spider tableau must have length 10.", nameof(tableau));
        }

        Options = options;
        Stock = stock;
        Tableau = tableau;
        CompletedSequences = completedSequences;
        Score = score;
    }

    /// <summary>The rules this game is played under.</summary>
    public SpiderOptions Options { get; }

    /// <summary>Face-down stock; index 0 is the next card dealt.</summary>
    public ImmutableArray<Card> Stock { get; }

    /// <summary>The ten tableau columns.</summary>
    public ImmutableArray<TableauPile> Tableau { get; }

    /// <summary>Number of completed K→A suit sequences (0..8).</summary>
    public int CompletedSequences { get; }

    /// <inheritdoc />
    public int Score { get; }

    /// <inheritdoc />
    public bool IsWon => CompletedSequences == TotalSequences;

    /// <summary>Returns a copy with the supplied fields replaced.</summary>
    public SpiderState With(
        ImmutableArray<Card>? stock = null,
        ImmutableArray<TableauPile>? tableau = null,
        int? completedSequences = null,
        int? score = null) =>
        new(
            Options,
            stock ?? Stock,
            tableau ?? Tableau,
            completedSequences ?? CompletedSequences,
            score ?? Score);
}
