using System.Collections.Immutable;
using System.Text;

namespace Solitaire.Engine.Tests;

/// <summary>
/// A small depth-first Klondike solver used only by the tests: it produces a
/// concrete winning move sequence for a given seed so that we can build a
/// genuine "expectedWin = true" test vector and exercise a full-game replay.
/// It is deliberately simple (heuristic move ordering + visited-state pruning +
/// a node budget) — it is not part of the shipped engine.
/// </summary>
public static class KlondikeSolver
{
    /// <summary>
    /// Attempts to find a winning move sequence. Returns null if none is found
    /// within <paramref name="nodeCap"/> explored states.
    /// </summary>
    public static IReadOnlyList<Move>? Solve(int seed, GameOptions options, int nodeCap = 400_000)
    {
        var start = Klondike.NewGame(seed, options);
        var visited = new HashSet<string>();
        var path = new List<Move>();
        int nodes = 0;

        return Dfs(start) ? path : null;

        bool Dfs(GameState state)
        {
            if (state.IsWon)
            {
                return true;
            }

            if (++nodes > nodeCap)
            {
                return false;
            }

            if (!visited.Add(Key(state)))
            {
                return false;
            }

            foreach (var move in OrderedMoves(state))
            {
                if (!Klondike.TryApplyMove(state, move, out var next, out _))
                {
                    continue;
                }

                path.Add(move);
                if (Dfs(next))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }

    private static IEnumerable<Move> OrderedMoves(GameState state) =>
        Klondike.GetLegalMoves(state)
            // Foundation -> tableau is almost never needed to win and greatly
            // widens the search, so the solver skips it.
            .Where(m => m.Type != MoveType.FoundationToTableau)
            .OrderByDescending(m => Priority(state, m));

    private static int Priority(GameState state, Move move) => move.Type switch
    {
        MoveType.TableauToFoundation => 100,
        MoveType.WasteToFoundation => 90,
        // A tableau move that empties its source's face-up run flips a card.
        MoveType.TableauToTableau when FlipsCard(state, move) => 80,
        MoveType.WasteToTableau => 60,
        MoveType.TableauToTableau => 40,
        MoveType.Draw => 20,
        MoveType.Recycle => 10,
        _ => 0,
    };

    private static bool FlipsCard(GameState state, Move move)
    {
        var source = state.Tableau[move.Source];
        return source.FaceDownCount > 0 && move.Count == source.FaceUpCount;
    }

    /// <summary>
    /// Canonical key for visited-state pruning. Redeal count is intentionally
    /// excluded so that returning to a previously seen board (e.g. after a full
    /// stock cycle) is pruned, preventing infinite draw/recycle loops.
    /// </summary>
    private static string Key(GameState state)
    {
        var sb = new StringBuilder();
        AppendCards(sb, state.Stock);
        sb.Append('|');
        AppendCards(sb, state.Waste);
        sb.Append('|');
        foreach (int rank in state.Foundations)
        {
            sb.Append(rank).Append(',');
        }

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
            sb.Append(card.OrdinalIndex).Append(',');
        }
    }
}
