using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// The authoritative, pure TriPeaks engine: deal, move validation/application,
/// legal-move generation, win detection, and deterministic replay. No method
/// mutates its inputs; every change yields a new <see cref="TriPeaksState"/>.
/// </summary>
/// <remarks>
/// Rules (per project decision): redeals are unlimited; the game is won once
/// the three peaks (28 tableau cards) are fully cleared (the stock/waste need
/// not empty). An exposed tableau card may be played onto the waste when its
/// rank is exactly one above or below the current waste-top card's rank, with
/// King/Ace wraparound allowed (King ↔ Ace counts as adjacent).
/// </remarks>
public static class TriPeaks
{
    /// <summary>
    /// Each non-base tableau index's two "children" in the row below — the
    /// cards it rests on and is covered by. Indices 0-2 are the three peak
    /// apexes; 3-8 the next row (2 per peak); 9-17 the row below that (3 per
    /// peak); 18-27 the shared 10-card base row, which has no children and is
    /// therefore always exposed (entry is null).
    /// </summary>
    private static readonly (int A, int B)?[] Children =
    {
        (3, 4), (5, 6), (7, 8),
        (9, 10), (10, 11), (12, 13), (13, 14), (15, 16), (16, 17),
        (18, 19), (19, 20), (20, 21), (21, 22), (22, 23), (23, 24), (24, 25), (25, 26), (26, 27),
        null, null, null, null, null, null, null, null, null, null,
    };

    /// <summary>Creates the initial game state for a seed.</summary>
    public static TriPeaksState NewGame(int seed)
    {
        var shuffled = Deck.Shuffle(seed);

        var tableau = ImmutableArray.CreateBuilder<Card?>(TriPeaksState.TableauSize);
        for (int i = 0; i < TriPeaksState.TableauSize; i++)
        {
            tableau.Add(shuffled[i]);
        }

        var waste = ImmutableArray.Create(shuffled[TriPeaksState.TableauSize]);

        var stock = ImmutableArray.CreateBuilder<Card>(Deck.Size - TriPeaksState.TableauSize - 1);
        for (int i = TriPeaksState.TableauSize + 1; i < Deck.Size; i++)
        {
            stock.Add(shuffled[i]);
        }

        return new TriPeaksState(tableau.MoveToImmutable(), stock.MoveToImmutable(), waste, score: 0);
    }

    /// <summary>True once the game is won (all three peaks are cleared).</summary>
    public static bool IsWin(TriPeaksState state) => state.IsWon;

    /// <summary>
    /// Validates <paramref name="move"/> against the rules of
    /// <paramref name="state"/>. On success returns true and yields the resulting
    /// <paramref name="next"/> state and the nominal <paramref name="scoreDelta"/>.
    /// On failure returns false and makes no changes.
    /// </summary>
    public static bool TryApplyMove(TriPeaksState state, TriPeaksMove move, out TriPeaksState next, out int scoreDelta)
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
    public static IReadOnlyList<TriPeaksMove> GetLegalMoves(TriPeaksState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var moves = new List<TriPeaksMove>();

        if (!state.Stock.IsEmpty)
        {
            moves.Add(TriPeaksMove.Draw());
        }
        else if (!state.Waste.IsEmpty)
        {
            moves.Add(TriPeaksMove.Recycle());
        }

        if (state.WasteTop is { } wasteTop)
        {
            for (int i = 0; i < TriPeaksState.TableauSize; i++)
            {
                if (IsExposed(state, i) && IsRankAdjacent(state.Tableau[i]!.Value.Rank, wasteTop.Rank))
                {
                    moves.Add(TriPeaksMove.PlayToWaste(i));
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
    public static ReplayResult<TriPeaksState> Replay(int seed, IReadOnlyList<TriPeaksMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var state = NewGame(seed);

        for (int i = 0; i < moves.Count; i++)
        {
            if (!TryApplyMove(state, moves[i], out var nextState, out _))
            {
                return new ReplayResult<TriPeaksState>(
                    FinalState: state,
                    Score: state.Score,
                    Won: false,
                    AllMovesLegal: false,
                    FirstIllegalMoveIndex: i);
            }

            state = nextState;
        }

        return new ReplayResult<TriPeaksState>(
            FinalState: state,
            Score: state.Score,
            Won: state.IsWon,
            AllMovesLegal: true,
            FirstIllegalMoveIndex: null);
    }

    // ----------------------------------------------------------------------
    // Portable move (de)serialization for vectors and the API.
    // ----------------------------------------------------------------------

    public static MoveDto ToDto(TriPeaksMove move) => move.Type switch
    {
        TriPeaksMoveType.Draw => new MoveDto("Draw"),
        TriPeaksMoveType.Recycle => new MoveDto("Recycle"),
        TriPeaksMoveType.PlayToWaste => new MoveDto("PlayToWaste", Source: move.Position),
        _ => throw new ArgumentOutOfRangeException(nameof(move), move.Type, "Unknown move type."),
    };

    public static TriPeaksMove FromDto(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Type switch
        {
            "Draw" => TriPeaksMove.Draw(),
            "Recycle" => TriPeaksMove.Recycle(),
            "PlayToWaste" => TriPeaksMove.PlayToWaste(Req(dto.Source, "source")),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Type, "Unknown move type."),
        };
    }

    private static int Req(int? value, string field) =>
        value ?? throw new ArgumentException($"TriPeaks move is missing '{field}'.");

    /// <summary>TriPeaks has no options; the bag is accepted but ignored (parity with other variants).</summary>
    public static TriPeaksOptions OptionsFromBag(IReadOnlyDictionary<string, int> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return default;
    }

    // ----------------------------------------------------------------------
    // Rule helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// True if the tableau slot at <paramref name="index"/> holds a card AND
    /// both of the cards it rests on in the row below (its "children") are gone
    /// — or it has no children (the shared base row), which is always exposed.
    /// </summary>
    private static bool IsExposed(TriPeaksState state, int index)
    {
        if (state.Tableau[index] is null)
        {
            return false;
        }

        var children = Children[index];
        if (children is null)
        {
            return true;
        }

        var (a, b) = children.Value;
        return state.Tableau[a] is null && state.Tableau[b] is null;
    }

    /// <summary>Adjacent by rank, one step either direction, with King↔Ace wraparound.</summary>
    private static bool IsRankAdjacent(int rankA, int rankB)
    {
        int diff = Math.Abs(rankA - rankB);
        return diff == 1 || diff == Card.King - Card.Ace;
    }

    // ----------------------------------------------------------------------
    // Move application. Returns null on any rule violation. Never mutates input.
    // ----------------------------------------------------------------------

    private static (TriPeaksState State, int ScoreDelta)? Apply(TriPeaksState state, TriPeaksMove move) => move.Type switch
    {
        TriPeaksMoveType.Draw => ApplyDraw(state),
        TriPeaksMoveType.Recycle => ApplyRecycle(state),
        TriPeaksMoveType.PlayToWaste => ApplyPlayToWaste(state, move),
        _ => null,
    };

    private static (TriPeaksState, int)? ApplyDraw(TriPeaksState state)
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

    private static (TriPeaksState, int)? ApplyRecycle(TriPeaksState state)
    {
        if (!state.Stock.IsEmpty || state.Waste.IsEmpty)
        {
            return null;
        }

        // Flipping the waste pile over restores the original draw order:
        // waste[0] (first drawn) becomes the new stock top. The waste is left
        // empty, so a Draw is needed before another card can be played.
        return (state.With(stock: state.Waste, waste: ImmutableArray<Card>.Empty), 0);
    }

    private static (TriPeaksState, int)? ApplyPlayToWaste(TriPeaksState state, TriPeaksMove move)
    {
        int position = move.Position;
        if (position < 0 || position >= TriPeaksState.TableauSize || !IsExposed(state, position))
        {
            return null;
        }

        if (state.WasteTop is not { } wasteTop)
        {
            return null;
        }

        var card = state.Tableau[position]!.Value;
        if (!IsRankAdjacent(card.Rank, wasteTop.Rank))
        {
            return null;
        }

        var newTableau = state.Tableau.SetItem(position, null);
        var newWaste = state.Waste.Add(card);
        return (state.With(tableau: newTableau, waste: newWaste), TriPeaksScoring.PlayToWaste);
    }
}
