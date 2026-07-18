using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// The authoritative, pure Pyramid engine: deal, move validation/application,
/// legal-move generation, win detection, and deterministic replay. No method
/// mutates its inputs; every change yields a new <see cref="PyramidState"/>.
/// </summary>
/// <remarks>
/// Rules (per project decision): redeals are unlimited; the game is won once
/// the 28-card triangle is fully cleared (the stock/waste need not empty).
/// Pairs summing to 13 are removed together; a lone King (rank 13) is removed
/// by itself. Only the pyramid's currently-exposed cards and the single waste
/// top card are ever eligible — the stock is always face-down/inaccessible.
/// </remarks>
public static class Pyramid
{
    /// <summary>Creates the initial game state for a seed.</summary>
    public static PyramidState NewGame(int seed)
    {
        var shuffled = Deck.Shuffle(seed);
        var pyramid = ImmutableArray.CreateBuilder<Card?>(PyramidState.PyramidSize);
        int next = 0;
        for (int row = 0; row < PyramidState.RowCount; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                pyramid.Add(shuffled[next++]);
            }
        }

        var stock = ImmutableArray.CreateBuilder<Card>(Deck.Size - next);
        for (int i = next; i < Deck.Size; i++)
        {
            stock.Add(shuffled[i]);
        }

        return new PyramidState(
            pyramid.MoveToImmutable(),
            stock.MoveToImmutable(),
            waste: ImmutableArray<Card>.Empty,
            score: 0);
    }

    /// <summary>True once the game is won (the whole triangle is cleared).</summary>
    public static bool IsWin(PyramidState state) => state.IsWon;

    /// <summary>
    /// Validates <paramref name="move"/> against the rules of
    /// <paramref name="state"/>. On success returns true and yields the resulting
    /// <paramref name="next"/> state and the nominal <paramref name="scoreDelta"/>.
    /// On failure returns false and makes no changes.
    /// </summary>
    public static bool TryApplyMove(PyramidState state, PyramidMove move, out PyramidState next, out int scoreDelta)
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
        next = partial.With(score: state.Score + delta);
        return true;
    }

    /// <summary>Returns every legal move from <paramref name="state"/> (used for hints).</summary>
    public static IReadOnlyList<PyramidMove> GetLegalMoves(PyramidState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var moves = new List<PyramidMove>();

        if (!state.Stock.IsEmpty)
        {
            moves.Add(PyramidMove.Draw());
        }
        else if (!state.Waste.IsEmpty)
        {
            moves.Add(PyramidMove.Recycle());
        }

        var exposed = new List<int>();
        for (int i = 0; i < PyramidState.PyramidSize; i++)
        {
            if (IsExposed(state, i))
            {
                exposed.Add(i);
            }
        }

        var wasteTop = state.WasteTop;

        // Lone Kings.
        foreach (int i in exposed)
        {
            if (state.Pyramid[i]!.Value.Rank == Card.King)
            {
                moves.Add(PyramidMove.RemoveSingle(i));
            }
        }

        if (wasteTop is { Rank: Card.King })
        {
            moves.Add(PyramidMove.RemoveSingle(PyramidMove.Waste));
        }

        // Pairs among exposed pyramid cards.
        for (int a = 0; a < exposed.Count; a++)
        {
            int rankA = state.Pyramid[exposed[a]]!.Value.Rank;
            if (rankA == Card.King)
            {
                continue;
            }

            for (int b = a + 1; b < exposed.Count; b++)
            {
                int rankB = state.Pyramid[exposed[b]]!.Value.Rank;
                if (rankA + rankB == 13)
                {
                    moves.Add(PyramidMove.RemovePair(exposed[a], exposed[b]));
                }
            }
        }

        // Pairs between an exposed pyramid card and the waste top.
        if (wasteTop is { } waste && waste.Rank != Card.King)
        {
            foreach (int i in exposed)
            {
                int rank = state.Pyramid[i]!.Value.Rank;
                if (rank != Card.King && rank + waste.Rank == 13)
                {
                    moves.Add(PyramidMove.RemovePair(i, PyramidMove.Waste));
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
    public static ReplayResult<PyramidState> Replay(int seed, IReadOnlyList<PyramidMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var state = NewGame(seed);

        for (int i = 0; i < moves.Count; i++)
        {
            if (!TryApplyMove(state, moves[i], out var nextState, out _))
            {
                return new ReplayResult<PyramidState>(
                    FinalState: state,
                    Score: state.Score,
                    Won: false,
                    AllMovesLegal: false,
                    FirstIllegalMoveIndex: i);
            }

            state = nextState;
        }

        return new ReplayResult<PyramidState>(
            FinalState: state,
            Score: state.Score,
            Won: state.IsWon,
            AllMovesLegal: true,
            FirstIllegalMoveIndex: null);
    }

    // ----------------------------------------------------------------------
    // Portable move (de)serialization for vectors and the API.
    // ----------------------------------------------------------------------

    public static MoveDto ToDto(PyramidMove move) => move.Type switch
    {
        PyramidMoveType.Draw => new MoveDto("Draw"),
        PyramidMoveType.Recycle => new MoveDto("Recycle"),
        PyramidMoveType.RemoveSingle => new MoveDto("RemoveSingle", Source: move.PositionA),
        PyramidMoveType.RemovePair =>
            new MoveDto("RemovePair", Source: move.PositionA, Destination: move.PositionB),
        _ => throw new ArgumentOutOfRangeException(nameof(move), move.Type, "Unknown move type."),
    };

    public static PyramidMove FromDto(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Type switch
        {
            "Draw" => PyramidMove.Draw(),
            "Recycle" => PyramidMove.Recycle(),
            "RemoveSingle" => PyramidMove.RemoveSingle(Req(dto.Source, "source")),
            "RemovePair" =>
                PyramidMove.RemovePair(Req(dto.Source, "source"), Req(dto.Destination, "destination")),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Type, "Unknown move type."),
        };
    }

    private static int Req(int? value, string field) =>
        value ?? throw new ArgumentException($"Pyramid move is missing '{field}'.");

    /// <summary>Pyramid has no options; the bag is accepted but ignored (parity with other variants).</summary>
    public static PyramidOptions OptionsFromBag(IReadOnlyDictionary<string, int> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return default;
    }

    // ----------------------------------------------------------------------
    // Rule helpers
    // ----------------------------------------------------------------------

    /// <summary>Flat index of row <paramref name="row"/>, column <paramref name="col"/> (both 0-based).</summary>
    private static int FlatIndex(int row, int col) => row * (row + 1) / 2 + col;

    /// <summary>The row (0..6) a flat pyramid index belongs to.</summary>
    private static int RowOf(int index)
    {
        int row = 0;
        while (FlatIndex(row + 1, 0) <= index)
        {
            row++;
        }

        return row;
    }

    /// <summary>
    /// True if the pyramid slot at <paramref name="index"/> holds a card AND
    /// both of the cards it rests on in the row below (its "children") are gone
    /// — or it is in the base row, which has nothing beneath it.
    /// </summary>
    private static bool IsExposed(PyramidState state, int index)
    {
        if (state.Pyramid[index] is null)
        {
            return false;
        }

        int row = RowOf(index);
        if (row == PyramidState.RowCount - 1)
        {
            return true;
        }

        int col = index - FlatIndex(row, 0);
        int childLeft = FlatIndex(row + 1, col);
        int childRight = FlatIndex(row + 1, col + 1);
        return state.Pyramid[childLeft] is null && state.Pyramid[childRight] is null;
    }

    /// <summary>Resolves a move position (pyramid slot or <see cref="PyramidMove.Waste"/>) to a card, if exposed/available.</summary>
    private static Card? ResolveExposedCard(PyramidState state, int position)
    {
        if (position == PyramidMove.Waste)
        {
            return state.WasteTop;
        }

        if (position < 0 || position >= PyramidState.PyramidSize)
        {
            return null;
        }

        return IsExposed(state, position) ? state.Pyramid[position] : null;
    }

    /// <summary>Removes the card at <paramref name="position"/> from wherever it lives.</summary>
    private static PyramidState RemoveAt(PyramidState state, int position)
    {
        if (position == PyramidMove.Waste)
        {
            return state.With(waste: state.Waste.RemoveAt(state.Waste.Length - 1));
        }

        return state.With(pyramid: state.Pyramid.SetItem(position, null));
    }

    // ----------------------------------------------------------------------
    // Move application. Returns null on any rule violation. Never mutates input.
    // ----------------------------------------------------------------------

    private static (PyramidState State, int ScoreDelta)? Apply(PyramidState state, PyramidMove move) => move.Type switch
    {
        PyramidMoveType.Draw => ApplyDraw(state),
        PyramidMoveType.Recycle => ApplyRecycle(state),
        PyramidMoveType.RemoveSingle => ApplyRemoveSingle(state, move),
        PyramidMoveType.RemovePair => ApplyRemovePair(state, move),
        _ => null,
    };

    private static (PyramidState, int)? ApplyDraw(PyramidState state)
    {
        if (state.Stock.IsEmpty)
        {
            return null;
        }

        var card = state.Stock[0];
        var newStock = state.Stock.RemoveAt(0);
        var newWaste = state.Waste.Add(card);
        return (state.With(stock: newStock, waste: newWaste), 0);
    }

    private static (PyramidState, int)? ApplyRecycle(PyramidState state)
    {
        if (!state.Stock.IsEmpty || state.Waste.IsEmpty)
        {
            return null;
        }

        // Flipping the waste pile over restores the original draw order:
        // waste[0] (first drawn) becomes the new stock top.
        return (state.With(stock: state.Waste, waste: ImmutableArray<Card>.Empty), 0);
    }

    private static (PyramidState, int)? ApplyRemoveSingle(PyramidState state, PyramidMove move)
    {
        var card = ResolveExposedCard(state, move.PositionA);
        if (card is not { Rank: Card.King })
        {
            return null;
        }

        return (RemoveAt(state, move.PositionA), PyramidScoring.RemoveSingle);
    }

    private static (PyramidState, int)? ApplyRemovePair(PyramidState state, PyramidMove move)
    {
        if (move.PositionA == move.PositionB)
        {
            return null;
        }

        var cardA = ResolveExposedCard(state, move.PositionA);
        var cardB = ResolveExposedCard(state, move.PositionB);
        if (cardA is not { } a || cardB is not { } b || a.Rank == Card.King || b.Rank == Card.King)
        {
            return null;
        }

        if (a.Rank + b.Rank != 13)
        {
            return null;
        }

        // Remove the higher position first so removing a pyramid slot can never
        // shift the meaning of the other position (positions are stable indices
        // into a flat array, not a list, so this ordering is actually not
        // required for correctness — kept for clarity/robustness regardless).
        var afterA = RemoveAt(state, move.PositionA);
        var afterBoth = RemoveAt(afterA, move.PositionB);
        return (afterBoth, PyramidScoring.RemovePair);
    }
}
