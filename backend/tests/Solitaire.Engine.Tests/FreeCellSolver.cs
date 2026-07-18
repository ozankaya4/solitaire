using System.Text;

namespace Solitaire.Engine.Tests;

/// <summary>
/// Test-only FreeCell helper: a deterministic, iterative greedy playthrough
/// generator used to build realistic (usually non-winning) vectors and to
/// exercise real move sequences. FreeCell's branching factor (8 tableau columns,
/// free cells, "any card on an empty column", and supermoves) makes it — like
/// Spider — not reliably solvable by a simple search within a small budget, so a
/// guaranteed-win vector is not generated here; win detection is covered
/// separately by unit tests against directly-constructed near-complete states
/// (see <c>FreeCellMoveLegalityTests.IsWon_*</c>). Mirrors <see cref="SpiderSolver"/>.
/// Not part of the shipped engine.
/// </summary>
public static class FreeCellSolver
{
    /// <summary>
    /// Plays a deterministic, cycle-free greedy line of up to <paramref name="maxMoves"/>
    /// moves (never revisiting a state). Always legal by construction.
    /// </summary>
    public static IReadOnlyList<FreeCellMove> GreedyPlaythrough(int seed, int maxMoves)
    {
        var state = FreeCell.NewGame(seed);
        var visited = new HashSet<string> { Key(state) };
        var moves = new List<FreeCellMove>();

        while (moves.Count < maxMoves)
        {
            FreeCellMove? chosen = null;
            FreeCellState? chosenNext = null;
            foreach (var move in OrderedMoves(state))
            {
                if (!FreeCell.TryApplyMove(state, move, out var next, out _))
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

    private static IEnumerable<FreeCellMove> OrderedMoves(FreeCellState state) =>
        FreeCell.GetLegalMoves(state)
            .Where(m => m.Type != FreeCellMoveType.FoundationToTableau)
            .OrderByDescending(m => Priority(m));

    private static int Priority(FreeCellMove move) => move.Type switch
    {
        FreeCellMoveType.TableauToFoundation => 100,
        FreeCellMoveType.FreeCellToFoundation => 95,
        FreeCellMoveType.TableauToTableau => 50 + move.Count,
        FreeCellMoveType.FreeCellToTableau => 40,
        FreeCellMoveType.TableauToFreeCell => 10,
        _ => 0,
    };

    private static string Key(FreeCellState state)
    {
        var sb = new StringBuilder();
        foreach (var cell in state.FreeCells)
        {
            if (cell is { } card)
            {
                sb.Append(card.OrdinalIndex);
            }
            else
            {
                sb.Append('_');
            }

            sb.Append(',');
        }

        sb.Append('|');
        foreach (int rank in state.Foundations)
        {
            sb.Append(rank).Append(',');
        }

        foreach (var pile in state.Tableau)
        {
            sb.Append('|');
            foreach (var card in pile.Cards)
            {
                sb.Append(card.OrdinalIndex).Append(',');
            }
        }

        return sb.ToString();
    }
}
