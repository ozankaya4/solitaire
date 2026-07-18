using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// The authoritative, pure FreeCell engine: deal, move validation/application,
/// legal-move generation, win detection, and deterministic replay. No method
/// mutates its inputs; every change yields a new <see cref="FreeCellState"/>.
/// </summary>
/// <remarks>
/// Rules: standard FreeCell. Tableau building is descending, alternating color
/// (same as Klondike), but unlike Klondike <b>any</b> card — not just a King —
/// may be placed on an empty column. A "supermove" (moving several cards at
/// once between tableau columns) is legal up to the number of cards that could
/// be relayed one at a time through the currently empty free cells and empty
/// tableau columns: <c>(1 + emptyFreeCells) * 2^emptyColumns</c>, where the
/// destination column (even if empty) does not count toward
/// <c>emptyColumns</c>. This is the standard convention used by essentially
/// every digital FreeCell implementation and is exactly equivalent to actually
/// performing the relay one card at a time.
/// </remarks>
public static class FreeCell
{
    /// <summary>Columns 0..3 get 7 cards; columns 4..7 get 6 cards (28 + 24 = 52).</summary>
    private static readonly int[] ColumnSizes = [7, 7, 7, 7, 6, 6, 6, 6];

    /// <summary>Creates the initial game state for a seed.</summary>
    public static FreeCellState NewGame(int seed)
    {
        var shuffled = Deck.Shuffle(seed);
        var tableau = ImmutableArray.CreateBuilder<TableauPile>(FreeCellState.TableauCount);
        int next = 0;
        foreach (int size in ColumnSizes)
        {
            var column = ImmutableArray.CreateBuilder<Card>(size);
            for (int i = 0; i < size; i++)
            {
                column.Add(shuffled[next++]);
            }

            // Every card is dealt face-up: FaceDownCount is always 0.
            tableau.Add(new TableauPile(column.MoveToImmutable(), 0));
        }

        return new FreeCellState(
            tableau.MoveToImmutable(),
            freeCells: ImmutableArray.Create<Card?>(null, null, null, null),
            foundations: ImmutableArray.Create(0, 0, 0, 0),
            score: 0);
    }

    /// <summary>True once the game is won (all foundations complete).</summary>
    public static bool IsWin(FreeCellState state) => state.IsWon;

    /// <summary>
    /// Validates <paramref name="move"/> against the rules of
    /// <paramref name="state"/>. On success returns true and yields the resulting
    /// <paramref name="next"/> state and the nominal <paramref name="scoreDelta"/>.
    /// On failure returns false and makes no changes.
    /// </summary>
    public static bool TryApplyMove(FreeCellState state, FreeCellMove move, out FreeCellState next, out int scoreDelta)
    {
        ArgumentNullException.ThrowIfNull(state);

        var result = Apply(state, move);
        if (result is null)
        {
            next = state;
            scoreDelta = 0;
            return false;
        }

        var (partial, delta) = result.Value;
        scoreDelta = delta;
        next = partial.With(score: FreeCellScoring.Clamp(state.Score + delta));
        return true;
    }

    /// <summary>Returns every legal move from <paramref name="state"/> (used for hints).</summary>
    public static IReadOnlyList<FreeCellMove> GetLegalMoves(FreeCellState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var moves = new List<FreeCellMove>();

        // Tableau top -> foundation.
        for (int s = 0; s < FreeCellState.TableauCount; s++)
        {
            if (state.Tableau[s].TopCard is { } top && CanPlaceOnFoundation(state, top))
            {
                moves.Add(FreeCellMove.TableauToFoundation(s));
            }
        }

        // Free cell -> foundation.
        for (int c = 0; c < FreeCellState.FreeCellCount; c++)
        {
            if (state.FreeCells[c] is { } card && CanPlaceOnFoundation(state, card))
            {
                moves.Add(FreeCellMove.FreeCellToFoundation(c));
            }
        }

        // Tableau top -> the first empty free cell (free cells are interchangeable).
        int firstEmptyCell = state.FreeCells.IndexOf(null);
        if (firstEmptyCell != -1)
        {
            for (int s = 0; s < FreeCellState.TableauCount; s++)
            {
                if (!state.Tableau[s].IsEmpty)
                {
                    moves.Add(FreeCellMove.TableauToFreeCell(s, firstEmptyCell));
                }
            }
        }

        // Free cell -> tableau.
        for (int c = 0; c < FreeCellState.FreeCellCount; c++)
        {
            if (state.FreeCells[c] is not { } card)
            {
                continue;
            }

            for (int t = 0; t < FreeCellState.TableauCount; t++)
            {
                if (CanPlaceOnTableau(state.Tableau[t], card))
                {
                    moves.Add(FreeCellMove.FreeCellToTableau(c, t));
                }
            }
        }

        // Foundation -> tableau.
        for (int f = 0; f < FreeCellState.FoundationCount; f++)
        {
            int rank = state.Foundations[f];
            if (rank == 0)
            {
                continue;
            }

            var card = new Card((Suit)f, rank);
            for (int t = 0; t < FreeCellState.TableauCount; t++)
            {
                if (CanPlaceOnTableau(state.Tableau[t], card))
                {
                    moves.Add(FreeCellMove.FoundationToTableau((Suit)f, t));
                }
            }
        }

        // Tableau -> tableau (every legal run length, bounded by the supermove limit).
        for (int s = 0; s < FreeCellState.TableauCount; s++)
        {
            var source = state.Tableau[s];
            for (int count = 1; count <= source.Count; count++)
            {
                var bottom = source.Cards[source.Count - count];
                for (int t = 0; t < FreeCellState.TableauCount; t++)
                {
                    if (t == s)
                    {
                        continue;
                    }

                    var dest = state.Tableau[t];

                    // Relocating an entire column onto another empty column never
                    // helps (every FreeCell card is already face-up; nothing is
                    // revealed) — prune it from suggestions.
                    if (dest.IsEmpty && count == source.Count)
                    {
                        continue;
                    }

                    if (CanPlaceOnTableau(dest, bottom)
                        && count <= MaxMovableCount(state, destinationIndex: t))
                    {
                        moves.Add(FreeCellMove.TableauToTableau(s, t, count));
                    }
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Replays a move sequence from a fresh game. Pure and side-effect free: the
    /// result reports whether every move was legal, the final state and score,
    /// and whether the game was completed as a win.
    /// </summary>
    public static ReplayResult<FreeCellState> Replay(int seed, IReadOnlyList<FreeCellMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var state = NewGame(seed);

        for (int i = 0; i < moves.Count; i++)
        {
            if (!TryApplyMove(state, moves[i], out var nextState, out _))
            {
                return new ReplayResult<FreeCellState>(
                    FinalState: state,
                    Score: state.Score,
                    Won: false,
                    AllMovesLegal: false,
                    FirstIllegalMoveIndex: i);
            }

            state = nextState;
        }

        return new ReplayResult<FreeCellState>(
            FinalState: state,
            Score: state.Score,
            Won: state.IsWon,
            AllMovesLegal: true,
            FirstIllegalMoveIndex: null);
    }

    // ----------------------------------------------------------------------
    // Portable move (de)serialization for vectors and the API.
    // ----------------------------------------------------------------------

    public static MoveDto ToDto(FreeCellMove move) => move.Type switch
    {
        FreeCellMoveType.TableauToTableau => new MoveDto(
            "TableauToTableau", Source: move.Source, Destination: move.Destination, Count: move.Count),
        FreeCellMoveType.TableauToFreeCell =>
            new MoveDto("TableauToFreeCell", Source: move.Source, Destination: move.Destination),
        FreeCellMoveType.TableauToFoundation => new MoveDto("TableauToFoundation", Source: move.Source),
        FreeCellMoveType.FreeCellToTableau =>
            new MoveDto("FreeCellToTableau", Source: move.Source, Destination: move.Destination),
        FreeCellMoveType.FreeCellToFoundation => new MoveDto("FreeCellToFoundation", Source: move.Source),
        FreeCellMoveType.FoundationToTableau =>
            new MoveDto("FoundationToTableau", Source: move.Source, Destination: move.Destination),
        _ => throw new ArgumentOutOfRangeException(nameof(move), move.Type, "Unknown move type."),
    };

    public static FreeCellMove FromDto(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Type switch
        {
            "TableauToTableau" => FreeCellMove.TableauToTableau(
                Req(dto.Source, "source"), Req(dto.Destination, "destination"), Req(dto.Count, "count")),
            "TableauToFreeCell" =>
                FreeCellMove.TableauToFreeCell(Req(dto.Source, "source"), Req(dto.Destination, "destination")),
            "TableauToFoundation" => FreeCellMove.TableauToFoundation(Req(dto.Source, "source")),
            "FreeCellToTableau" =>
                FreeCellMove.FreeCellToTableau(Req(dto.Source, "source"), Req(dto.Destination, "destination")),
            "FreeCellToFoundation" => FreeCellMove.FreeCellToFoundation(Req(dto.Source, "source")),
            "FoundationToTableau" =>
                FreeCellMove.FoundationToTableau((Suit)Req(dto.Source, "source"), Req(dto.Destination, "destination")),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Type, "Unknown move type."),
        };
    }

    private static int Req(int? value, string field) =>
        value ?? throw new ArgumentException($"FreeCell move is missing '{field}'.");

    /// <summary>FreeCell has no options; the bag is accepted but ignored (parity with other variants).</summary>
    public static FreeCellOptions OptionsFromBag(IReadOnlyDictionary<string, int> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return default;
    }

    // ----------------------------------------------------------------------
    // Rule helpers
    // ----------------------------------------------------------------------

    private static bool CanPlaceOnFoundation(FreeCellState state, Card moving) =>
        state.Foundations[(int)moving.Suit] == moving.Rank - 1;

    /// <summary>
    /// Can <paramref name="moving"/> be placed on <paramref name="pile"/>? Any
    /// card on an empty pile (unlike Klondike); otherwise one rank below the top
    /// card and of the opposite color.
    /// </summary>
    private static bool CanPlaceOnTableau(TableauPile pile, Card moving)
    {
        if (pile.IsEmpty)
        {
            return true;
        }

        Card top = pile.Cards[^1];
        return moving.Rank == top.Rank - 1 && moving.Color != top.Color;
    }

    private static bool IsValidRun(ReadOnlySpan<Card> run)
    {
        for (int i = 1; i < run.Length; i++)
        {
            Card lower = run[i];
            Card upper = run[i - 1];
            if (lower.Rank != upper.Rank - 1 || lower.Color == upper.Color)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The largest run that can legally move to <paramref name="destinationIndex"/>
    /// right now: <c>(1 + emptyFreeCells) * 2^emptyColumns</c>, where the
    /// destination column itself (even if empty) is excluded from the empty-column
    /// count — it cannot serve as scratch space for its own incoming run.
    /// </summary>
    private static int MaxMovableCount(FreeCellState state, int destinationIndex)
    {
        int emptyFreeCells = state.EmptyFreeCellCount;
        int emptyColumns = 0;
        for (int t = 0; t < FreeCellState.TableauCount; t++)
        {
            if (t != destinationIndex && state.Tableau[t].IsEmpty)
            {
                emptyColumns++;
            }
        }

        return (1 + emptyFreeCells) * (1 << emptyColumns);
    }

    private static bool IsTableauIndex(int index) => index is >= 0 and < FreeCellState.TableauCount;
    private static bool IsFreeCellIndex(int index) => index is >= 0 and < FreeCellState.FreeCellCount;

    // ----------------------------------------------------------------------
    // Move application. Returns null on any rule violation. Never mutates input.
    // ----------------------------------------------------------------------

    private static (FreeCellState State, int ScoreDelta)? Apply(FreeCellState state, FreeCellMove move) => move.Type switch
    {
        FreeCellMoveType.TableauToTableau => ApplyTableauToTableau(state, move),
        FreeCellMoveType.TableauToFreeCell => ApplyTableauToFreeCell(state, move),
        FreeCellMoveType.TableauToFoundation => ApplyTableauToFoundation(state, move),
        FreeCellMoveType.FreeCellToTableau => ApplyFreeCellToTableau(state, move),
        FreeCellMoveType.FreeCellToFoundation => ApplyFreeCellToFoundation(state, move),
        FreeCellMoveType.FoundationToTableau => ApplyFoundationToTableau(state, move),
        _ => null,
    };

    private static (FreeCellState, int)? ApplyTableauToTableau(FreeCellState state, FreeCellMove move)
    {
        if (!IsTableauIndex(move.Source) || !IsTableauIndex(move.Destination) || move.Source == move.Destination)
        {
            return null;
        }

        var source = state.Tableau[move.Source];
        if (move.Count < 1 || move.Count > source.Count)
        {
            return null;
        }

        var moved = source.Cards.AsSpan(source.Count - move.Count, move.Count);
        if (!IsValidRun(moved))
        {
            return null;
        }

        var dest = state.Tableau[move.Destination];
        if (!CanPlaceOnTableau(dest, moved[0]) || move.Count > MaxMovableCount(state, move.Destination))
        {
            return null;
        }

        var newSource = source.RemoveTop(move.Count, out _); // no flips: FreeCell has no face-down cards
        var newDest = dest.Append(moved);
        var newTableau = state.Tableau
            .SetItem(move.Source, newSource)
            .SetItem(move.Destination, newDest);

        return (state.With(tableau: newTableau), 0);
    }

    private static (FreeCellState, int)? ApplyTableauToFreeCell(FreeCellState state, FreeCellMove move)
    {
        if (!IsTableauIndex(move.Source) || !IsFreeCellIndex(move.Destination))
        {
            return null;
        }

        var source = state.Tableau[move.Source];
        if (source.TopCard is not { } card || state.FreeCells[move.Destination] is not null)
        {
            return null;
        }

        var newSource = source.RemoveTop(1, out _);
        var newTableau = state.Tableau.SetItem(move.Source, newSource);
        var newFreeCells = state.FreeCells.SetItem(move.Destination, card);

        return (state.With(tableau: newTableau, freeCells: newFreeCells), 0);
    }

    private static (FreeCellState, int)? ApplyTableauToFoundation(FreeCellState state, FreeCellMove move)
    {
        if (!IsTableauIndex(move.Source))
        {
            return null;
        }

        var source = state.Tableau[move.Source];
        if (source.TopCard is not { } card || !CanPlaceOnFoundation(state, card))
        {
            return null;
        }

        var newSource = source.RemoveTop(1, out _);
        var newFoundations = state.Foundations.SetItem((int)card.Suit, card.Rank);
        var newTableau = state.Tableau.SetItem(move.Source, newSource);

        return (state.With(tableau: newTableau, foundations: newFoundations), FreeCellScoring.ToFoundation);
    }

    private static (FreeCellState, int)? ApplyFreeCellToTableau(FreeCellState state, FreeCellMove move)
    {
        if (!IsFreeCellIndex(move.Source) || !IsTableauIndex(move.Destination))
        {
            return null;
        }

        if (state.FreeCells[move.Source] is not { } card)
        {
            return null;
        }

        var dest = state.Tableau[move.Destination];
        if (!CanPlaceOnTableau(dest, card))
        {
            return null;
        }

        var newTableau = state.Tableau.SetItem(move.Destination, dest.Append([card]));
        var newFreeCells = state.FreeCells.SetItem(move.Source, null);

        return (state.With(tableau: newTableau, freeCells: newFreeCells), 0);
    }

    private static (FreeCellState, int)? ApplyFreeCellToFoundation(FreeCellState state, FreeCellMove move)
    {
        if (!IsFreeCellIndex(move.Source))
        {
            return null;
        }

        if (state.FreeCells[move.Source] is not { } card || !CanPlaceOnFoundation(state, card))
        {
            return null;
        }

        var newFoundations = state.Foundations.SetItem((int)card.Suit, card.Rank);
        var newFreeCells = state.FreeCells.SetItem(move.Source, null);

        return (state.With(freeCells: newFreeCells, foundations: newFoundations), FreeCellScoring.ToFoundation);
    }

    private static (FreeCellState, int)? ApplyFoundationToTableau(FreeCellState state, FreeCellMove move)
    {
        if (move.Source is < 0 or >= FreeCellState.FoundationCount || !IsTableauIndex(move.Destination))
        {
            return null;
        }

        int rank = state.Foundations[move.Source];
        if (rank == 0)
        {
            return null;
        }

        var card = new Card((Suit)move.Source, rank);
        var dest = state.Tableau[move.Destination];
        if (!CanPlaceOnTableau(dest, card))
        {
            return null;
        }

        var newFoundations = state.Foundations.SetItem(move.Source, rank - 1);
        var newTableau = state.Tableau.SetItem(move.Destination, dest.Append([card]));

        return (state.With(tableau: newTableau, foundations: newFoundations), FreeCellScoring.FoundationToTableau);
    }
}
