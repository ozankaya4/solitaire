using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// An immutable snapshot of a Klondike game. Every mutation produces a new
/// <see cref="GameState"/>, so replay and move validation are side-effect free.
/// </summary>
/// <remarks>
/// Orientation conventions (part of the internal model; not exposed in vectors):
/// <list type="bullet">
/// <item><see cref="Stock"/> — index 0 is the top (the next card drawn).</item>
/// <item><see cref="Waste"/> — the last element is the top (the playable card).</item>
/// <item><see cref="Foundations"/> — length 4, indexed by <see cref="Suit"/>; the
/// value is the highest rank present (0 = empty, 13 = complete).</item>
/// <item><see cref="Tableau"/> — length 7; see <see cref="TableauPile"/>.</item>
/// </list>
/// </remarks>
public sealed class GameState : IGameState
{
    /// <summary>Number of tableau columns in Klondike.</summary>
    public const int TableauCount = 7;

    /// <summary>Number of foundations (one per suit).</summary>
    public const int FoundationCount = 4;

    public GameState(
        GameOptions options,
        ImmutableArray<Card> stock,
        ImmutableArray<Card> waste,
        ImmutableArray<int> foundations,
        ImmutableArray<TableauPile> tableau,
        int score,
        int redealsUsed)
    {
        if (foundations.Length != FoundationCount)
        {
            throw new ArgumentException("Foundations must have length 4.", nameof(foundations));
        }

        if (tableau.Length != TableauCount)
        {
            throw new ArgumentException("Tableau must have length 7.", nameof(tableau));
        }

        Options = options;
        Stock = stock;
        Waste = waste;
        Foundations = foundations;
        Tableau = tableau;
        Score = score;
        RedealsUsed = redealsUsed;
    }

    /// <summary>The rules this game is played under.</summary>
    public GameOptions Options { get; }

    /// <summary>Face-down draw pile; index 0 is the top (next drawn).</summary>
    public ImmutableArray<Card> Stock { get; }

    /// <summary>Face-up discard pile; the last element is the playable top card.</summary>
    public ImmutableArray<Card> Waste { get; }

    /// <summary>Top rank per suit-indexed foundation (0 = empty).</summary>
    public ImmutableArray<int> Foundations { get; }

    /// <summary>The seven tableau columns.</summary>
    public ImmutableArray<TableauPile> Tableau { get; }

    /// <summary>Current running score (never negative).</summary>
    public int Score { get; }

    /// <summary>How many times the waste has been recycled so far.</summary>
    public int RedealsUsed { get; }

    /// <summary>The playable top waste card, or null when the waste is empty.</summary>
    public Card? WasteTop => Waste.IsEmpty ? null : Waste[^1];

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

    /// <summary>Returns a copy with the supplied fields replaced.</summary>
    public GameState With(
        ImmutableArray<Card>? stock = null,
        ImmutableArray<Card>? waste = null,
        ImmutableArray<int>? foundations = null,
        ImmutableArray<TableauPile>? tableau = null,
        int? score = null,
        int? redealsUsed = null) =>
        new(
            Options,
            stock ?? Stock,
            waste ?? Waste,
            foundations ?? Foundations,
            tableau ?? Tableau,
            score ?? Score,
            redealsUsed ?? RedealsUsed);
}
