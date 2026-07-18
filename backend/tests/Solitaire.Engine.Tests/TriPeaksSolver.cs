using System.Text;

namespace Solitaire.Engine.Tests;

/// <summary>
/// Test-only TriPeaks helper: a deterministic, iterative greedy playthrough
/// generator used to build realistic (usually non-winning) vectors and to
/// exercise real move sequences. Mirrors <see cref="PyramidSolver"/> /
/// <see cref="SpiderSolver"/> — a plain greedy line, not a backtracking
/// solver, is enough for cross-language vector coverage and avoids betting on
/// a small search budget reliably finding a genuine win. Not part of the
/// shipped engine.
/// </summary>
public static class TriPeaksSolver
{
    /// <summary>
    /// Plays a deterministic, cycle-free greedy line of up to <paramref name="maxMoves"/>
    /// moves (never revisiting a state). Always legal by construction.
    /// </summary>
    public static IReadOnlyList<TriPeaksMove> GreedyPlaythrough(int seed, int maxMoves)
    {
        var state = TriPeaks.NewGame(seed);
        var visited = new HashSet<string> { Key(state) };
        var moves = new List<TriPeaksMove>();

        while (moves.Count < maxMoves)
        {
            TriPeaksMove? chosen = null;
            TriPeaksState? chosenNext = null;
            foreach (var move in OrderedMoves(state))
            {
                if (!TriPeaks.TryApplyMove(state, move, out var next, out _))
                {
                    continue;
                }

                if (!visited.Add(Key(next)))
                {
                    continue; // would revisit a state; skip to avoid cycles
                }

                chosen = move;
                chosenNext = next;
                break;
            }

            if (chosen is null || chosenNext is null)
            {
                break;
            }

            moves.Add(chosen.Value);
            state = chosenNext;
            if (state.IsWon)
            {
                break;
            }
        }

        return moves;
    }

    private static IEnumerable<TriPeaksMove> OrderedMoves(TriPeaksState state) =>
        TriPeaks.GetLegalMoves(state).OrderByDescending(m => Priority(m));

    private static int Priority(TriPeaksMove move) => move.Type switch
    {
        TriPeaksMoveType.PlayToWaste => 100,
        TriPeaksMoveType.Draw => 20,
        TriPeaksMoveType.Recycle => 10,
        _ => 0,
    };

    private static string Key(TriPeaksState state)
    {
        var sb = new StringBuilder();
        foreach (var card in state.Tableau)
        {
            if (card is { } c)
            {
                sb.Append(c.OrdinalIndex);
            }
            else
            {
                sb.Append('_');
            }

            sb.Append(',');
        }

        sb.Append('|');
        foreach (var card in state.Stock)
        {
            sb.Append(card.OrdinalIndex).Append(',');
        }

        sb.Append('|');
        foreach (var card in state.Waste)
        {
            sb.Append(card.OrdinalIndex).Append(',');
        }

        return sb.ToString();
    }
}
