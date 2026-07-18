using System.Text;

namespace Solitaire.Engine.Tests;

/// <summary>
/// Test-only Pyramid helper: a deterministic, iterative greedy playthrough
/// generator used to build realistic (usually non-winning) vectors and to
/// exercise real move sequences. Mirrors <see cref="SpiderSolver"/> /
/// <see cref="FreeCellSolver"/> — a plain greedy line, not a backtracking
/// solver, is enough for cross-language vector coverage and avoids betting on
/// a small search budget reliably finding a genuine win. Not part of the
/// shipped engine.
/// </summary>
public static class PyramidSolver
{
    /// <summary>
    /// Plays a deterministic, cycle-free greedy line of up to <paramref name="maxMoves"/>
    /// moves (never revisiting a state). Always legal by construction.
    /// </summary>
    public static IReadOnlyList<PyramidMove> GreedyPlaythrough(int seed, int maxMoves)
    {
        var state = Pyramid.NewGame(seed);
        var visited = new HashSet<string> { Key(state) };
        var moves = new List<PyramidMove>();

        while (moves.Count < maxMoves)
        {
            PyramidMove? chosen = null;
            PyramidState? chosenNext = null;
            foreach (var move in OrderedMoves(state))
            {
                if (!Pyramid.TryApplyMove(state, move, out var next, out _))
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

    private static IEnumerable<PyramidMove> OrderedMoves(PyramidState state) =>
        Pyramid.GetLegalMoves(state).OrderByDescending(m => Priority(m));

    private static int Priority(PyramidMove move) => move.Type switch
    {
        PyramidMoveType.RemovePair => 100,
        PyramidMoveType.RemoveSingle => 90,
        PyramidMoveType.Draw => 20,
        PyramidMoveType.Recycle => 10,
        _ => 0,
    };

    private static string Key(PyramidState state)
    {
        var sb = new StringBuilder();
        foreach (var card in state.Pyramid)
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
