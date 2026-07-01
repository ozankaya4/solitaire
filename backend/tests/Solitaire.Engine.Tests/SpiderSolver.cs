using System.Collections.Immutable;
using System.Text;

namespace Solitaire.Engine.Tests;

/// <summary>
/// Test-only Spider helper: a deterministic, iterative greedy playthrough
/// generator used to build realistic (usually non-winning) vectors and to
/// exercise real move sequences. Spider — especially 4-suit — is not reliably
/// solvable by a simple search, so full-win vectors are not generated here; win
/// detection is covered separately via constructed near-complete states.
/// Not part of the shipped engine.
/// </summary>
public static class SpiderSolver
{
    /// <summary>
    /// Plays a deterministic, cycle-free greedy line of up to <paramref name="maxMoves"/>
    /// moves (never revisiting a state). Always legal by construction.
    /// </summary>
    public static IReadOnlyList<SpiderMove> GreedyPlaythrough(int seed, SpiderOptions options, int maxMoves)
    {
        var state = Spider.NewGame(seed, options);
        var visited = new HashSet<string> { Key(state) };
        var moves = new List<SpiderMove>();

        while (moves.Count < maxMoves)
        {
            SpiderMove? chosen = null;
            foreach (var move in OrderedMoves(state))
            {
                if (!Spider.TryApplyMove(state, move, out var next, out _))
                {
                    continue;
                }

                if (!visited.Add(Key(next)))
                {
                    continue; // would revisit a state; skip to avoid cycles
                }

                chosen = move;
                state = next;
                break;
            }

            if (chosen is null)
            {
                break;
            }

            moves.Add(chosen.Value);
            if (state.IsWon)
            {
                break;
            }
        }

        return moves;
    }

    private static IEnumerable<SpiderMove> OrderedMoves(SpiderState state) =>
        Spider.GetLegalMoves(state).OrderByDescending(m => Priority(state, m));

    private static int Priority(SpiderState state, SpiderMove move)
    {
        if (move.Type == SpiderMoveType.Deal)
        {
            return 10;
        }

        var source = state.Tableau[move.Source];
        var dest = state.Tableau[move.Destination];
        var bottom = source.Cards[source.Count - move.Count];

        if (!dest.IsEmpty && dest.Cards[^1].Suit == bottom.Suit)
        {
            return 100; // in-suit build — progresses toward a completed sequence
        }

        if (source.FaceDownCount > 0 && move.Count == source.FaceUpCount)
        {
            return 80; // exposes a hidden card
        }

        return dest.IsEmpty ? 30 : 50;
    }

    private static string Key(SpiderState state)
    {
        var sb = new StringBuilder();
        AppendCards(sb, state.Stock);
        foreach (var pile in state.Tableau)
        {
            sb.Append('|').Append(pile.FaceDownCount).Append(':');
            AppendCards(sb, pile.Cards);
        }

        return sb.ToString();
    }

    private static void AppendCards(StringBuilder sb, ImmutableArray<Card> cards)
    {
        foreach (var card in cards)
        {
            sb.Append((int)card.Suit).Append('.').Append(card.Rank).Append(',');
        }
    }
}
