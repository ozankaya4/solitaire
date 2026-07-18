using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// An immutable snapshot of a FreeCell game. Every mutation produces a new
/// <see cref="FreeCellState"/>, so replay and move validation are side-effect free.
/// </summary>
/// <remarks>
/// Orientation conventions:
/// <list type="bullet">
/// <item><see cref="Tableau"/> — length 8; all cards are always face-up (dealt
/// face-up, and FreeCell has no face-down cards at all), so every
/// <see cref="TableauPile"/> here has <c>FaceDownCount == 0</c>.</item>
/// <item><see cref="FreeCells"/> — length 4; each slot holds at most one card.</item>
/// <item><see cref="Foundations"/> — length 4, indexed by <see cref="Suit"/>; the
/// value is the highest rank present (0 = empty, 13 = complete).</item>
/// </list>
/// </remarks>
public sealed class FreeCellState : IGameState
{
    /// <summary>Number of tableau columns in FreeCell.</summary>
    public const int TableauCount = 8;

    /// <summary>Number of free cells.</summary>
    public const int FreeCellCount = 4;

    /// <summary>Number of foundations (one per suit).</summary>
    public const int FoundationCount = 4;

    public FreeCellState(
        ImmutableArray<TableauPile> tableau,
        ImmutableArray<Card?> freeCells,
        ImmutableArray<int> foundations,
        int score)
    {
        if (tableau.Length != TableauCount)
        {
            throw new ArgumentException("Tableau must have length 8.", nameof(tableau));
        }

        if (freeCells.Length != FreeCellCount)
        {
            throw new ArgumentException("FreeCells must have length 4.", nameof(freeCells));
        }

        if (foundations.Length != FoundationCount)
        {
            throw new ArgumentException("Foundations must have length 4.", nameof(foundations));
        }

        Tableau = tableau;
        FreeCells = freeCells;
        Foundations = foundations;
        Score = score;
    }

    /// <summary>The eight tableau columns. Every card in every pile is face-up.</summary>
    public ImmutableArray<TableauPile> Tableau { get; }

    /// <summary>The four free cells; null means empty.</summary>
    public ImmutableArray<Card?> FreeCells { get; }

    /// <summary>Top rank per suit-indexed foundation (0 = empty).</summary>
    public ImmutableArray<int> Foundations { get; }

    /// <summary>Current running score (never negative).</summary>
    public int Score { get; }

    /// <summary>True once all four foundations are complete (King on top).</summary>
    public bool IsWon
    {
        get
        {
            foreach (int top in Foundations)
            {
                if (top != Card.King)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Number of currently empty free cells.</summary>
    public int EmptyFreeCellCount => FreeCells.Count(c => c is null);

    /// <summary>Returns a copy with the supplied fields replaced.</summary>
    public FreeCellState With(
        ImmutableArray<TableauPile>? tableau = null,
        ImmutableArray<Card?>? freeCells = null,
        ImmutableArray<int>? foundations = null,
        int? score = null) =>
        new(
            tableau ?? Tableau,
            freeCells ?? FreeCells,
            foundations ?? Foundations,
            score ?? Score);
}
