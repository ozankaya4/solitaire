using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// The authoritative, pure Klondike engine: deal, move validation/application,
/// legal-move generation, win detection, and deterministic replay. No method
/// mutates its inputs; every change yields a new <see cref="GameState"/>.
/// </summary>
public static class Klondike
{
    /// <summary>Creates the initial game state for a seed and rule set.</summary>
    public static GameState NewGame(int seed, GameOptions options)
    {
        options.Validate();

        var shuffled = Deck.Shuffle(seed);
        var (tableau, stock) = Deck.Deal(shuffled);

        return new GameState(
            options,
            stock: stock,
            waste: ImmutableArray<Card>.Empty,
            foundations: ImmutableArray.Create(0, 0, 0, 0),
            tableau: tableau,
            score: 0,
            redealsUsed: 0);
    }

    /// <summary>True once the game is won (all foundations complete).</summary>
    public static bool IsWin(GameState state) => state.IsWon;

    /// <summary>
    /// Validates <paramref name="move"/> against the rules of
    /// <paramref name="state"/>. On success returns true and yields the resulting
    /// <paramref name="next"/> state and the nominal <paramref name="scoreDelta"/>.
    /// On failure returns false, sets <paramref name="next"/> to the unchanged
    /// input state, and makes no changes (no mutation on failure).
    /// </summary>
    /// <remarks>
    /// <paramref name="scoreDelta"/> is the nominal points for the move; the
    /// cumulative <see cref="GameState.Score"/> is clamped so it never goes below
    /// zero, so a large penalty may change the score by less than the delta.
    /// </remarks>
    public static bool TryApplyMove(GameState state, Move move, out GameState next, out int scoreDelta)
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
        next = partial.With(score: Scoring.Clamp(state.Score + delta));
        return true;
    }

    /// <summary>
    /// Returns every legal move from <paramref name="state"/> (used for hints).
    /// </summary>
    /// <remarks>
    /// One pruning rule keeps the list useful: moving an entire face-up-only
    /// column (no face-down cards beneath) onto an empty column is omitted because
    /// it relocates a King without exposing anything. Such a move is still
    /// <em>accepted</em> by <see cref="TryApplyMove"/>; it is merely not suggested.
    /// </remarks>
    public static IReadOnlyList<Move> GetLegalMoves(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var moves = new List<Move>();

        // Stock / waste cycling.
        if (!state.Stock.IsEmpty)
        {
            moves.Add(Move.Draw());
        }
        else if (!state.Waste.IsEmpty && state.RedealsUsed < state.Options.MaxRedeals)
        {
            moves.Add(Move.Recycle());
        }

        // Waste top plays.
        if (state.WasteTop is { } wasteCard)
        {
            if (CanPlaceOnFoundation(state, wasteCard))
            {
                moves.Add(Move.WasteToFoundation());
            }

            for (int t = 0; t < GameState.TableauCount; t++)
            {
                if (CanPlaceOnTableau(state.Tableau[t], wasteCard))
                {
                    moves.Add(Move.WasteToTableau(t));
                }
            }
        }

        // Tableau top -> foundation.
        for (int s = 0; s < GameState.TableauCount; s++)
        {
            if (state.Tableau[s].TopCard is { } top && CanPlaceOnFoundation(state, top))
            {
                moves.Add(Move.TableauToFoundation(s));
            }
        }

        // Foundation -> tableau.
        for (int f = 0; f < GameState.FoundationCount; f++)
        {
            int rank = state.Foundations[f];
            if (rank == 0)
            {
                continue;
            }

            var card = new Card((Suit)f, rank);
            for (int t = 0; t < GameState.TableauCount; t++)
            {
                if (CanPlaceOnTableau(state.Tableau[t], card))
                {
                    moves.Add(Move.FoundationToTableau((Suit)f, t));
                }
            }
        }

        // Tableau -> tableau (every legal run length).
        for (int s = 0; s < GameState.TableauCount; s++)
        {
            var source = state.Tableau[s];
            for (int count = 1; count <= source.FaceUpCount; count++)
            {
                var bottom = source.Cards[source.Count - count];
                for (int t = 0; t < GameState.TableauCount; t++)
                {
                    if (t == s)
                    {
                        continue;
                    }

                    var dest = state.Tableau[t];

                    // Skip relocating a whole face-up-only column onto an empty one.
                    if (dest.IsEmpty
                        && source.FaceDownCount == 0
                        && count == source.FaceUpCount)
                    {
                        continue;
                    }

                    if (CanPlaceOnTableau(dest, bottom))
                    {
                        moves.Add(Move.TableauToTableau(s, t, count));
                    }
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Replays a move sequence from a fresh game. Pure and side-effect free: the
    /// result reports whether every move was legal, the final state and score, and
    /// whether the game was completed as a win. Any illegal move stops the replay
    /// and is reported via <see cref="ReplayResult.FirstIllegalMoveIndex"/> — this
    /// is the anti-cheat guarantee.
    /// </summary>
    public static ReplayResult<GameState> Replay(int seed, GameOptions options, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var state = NewGame(seed, options);

        for (int i = 0; i < moves.Count; i++)
        {
            if (!TryApplyMove(state, moves[i], out var nextState, out _))
            {
                return new ReplayResult<GameState>(
                    FinalState: state,
                    Score: state.Score,
                    Won: false,
                    AllMovesLegal: false,
                    FirstIllegalMoveIndex: i);
            }

            state = nextState;
        }

        return new ReplayResult<GameState>(
            FinalState: state,
            Score: state.Score,
            Won: state.IsWon,
            AllMovesLegal: true,
            FirstIllegalMoveIndex: null);
    }

    // ----------------------------------------------------------------------
    // Portable move (de)serialization for vectors and the API.
    // ----------------------------------------------------------------------

    /// <summary>Converts a Klondike move to its portable <see cref="MoveDto"/> form.</summary>
    public static MoveDto ToDto(Move move) => move.Type switch
    {
        MoveType.Draw => new MoveDto("Draw"),
        MoveType.Recycle => new MoveDto("Recycle"),
        MoveType.WasteToFoundation => new MoveDto("WasteToFoundation"),
        MoveType.WasteToTableau => new MoveDto("WasteToTableau", Destination: move.Destination),
        MoveType.TableauToFoundation => new MoveDto("TableauToFoundation", Source: move.Source),
        MoveType.FoundationToTableau =>
            new MoveDto("FoundationToTableau", Source: move.Source, Destination: move.Destination),
        MoveType.TableauToTableau => new MoveDto(
            "TableauToTableau", Source: move.Source, Destination: move.Destination, Count: move.Count),
        _ => throw new ArgumentOutOfRangeException(nameof(move), move.Type, "Unknown move type."),
    };

    /// <summary>Parses a portable <see cref="MoveDto"/> into a Klondike move.</summary>
    public static Move FromDto(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Type switch
        {
            "Draw" => Move.Draw(),
            "Recycle" => Move.Recycle(),
            "WasteToFoundation" => Move.WasteToFoundation(),
            "WasteToTableau" => Move.WasteToTableau(Req(dto.Destination, "destination")),
            "TableauToFoundation" => Move.TableauToFoundation(Req(dto.Source, "source")),
            "FoundationToTableau" =>
                Move.FoundationToTableau((Suit)Req(dto.Source, "source"), Req(dto.Destination, "destination")),
            "TableauToTableau" => Move.TableauToTableau(
                Req(dto.Source, "source"), Req(dto.Destination, "destination"), Req(dto.Count, "count")),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Type, "Unknown move type."),
        };
    }

    private static int Req(int? value, string field) =>
        value ?? throw new ArgumentException($"Klondike move is missing '{field}'.");

    /// <summary>Reads Klondike options from a portable options bag.</summary>
    public static GameOptions OptionsFromBag(IReadOnlyDictionary<string, int> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.TryGetValue("drawCount", out int drawCount))
        {
            throw new ArgumentException("Klondike options require 'drawCount'.");
        }

        int maxRedeals = options.TryGetValue("maxRedeals", out int mr) ? mr : GameOptions.Unlimited;
        var result = new GameOptions(drawCount, maxRedeals);
        result.Validate();
        return result;
    }

    // ----------------------------------------------------------------------
    // Rule helpers
    // ----------------------------------------------------------------------

    /// <summary>Can <paramref name="moving"/> be placed on foundation of its suit?</summary>
    private static bool CanPlaceOnFoundation(GameState state, Card moving) =>
        state.Foundations[(int)moving.Suit] == moving.Rank - 1;

    /// <summary>
    /// Can <paramref name="moving"/> (the bottom card of the moved run) be placed
    /// on <paramref name="pile"/>? Kings on empty piles; otherwise one rank below
    /// the top card and of the opposite color.
    /// </summary>
    private static bool CanPlaceOnTableau(TableauPile pile, Card moving)
    {
        if (pile.IsEmpty)
        {
            return moving.Rank == Card.King;
        }

        Card top = pile.Cards[^1];
        return moving.Rank == top.Rank - 1 && moving.Color != top.Color;
    }

    /// <summary>True if the face-up cards <paramref name="run"/> form an alternating,
    /// strictly-descending sequence from bottom (index 0) to top.</summary>
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

    // ----------------------------------------------------------------------
    // Move application. Returns null on any rule violation. Never mutates input.
    // The returned state carries the *unclamped* running score is left to the
    // caller; here we only compute the state delta and the nominal points.
    // ----------------------------------------------------------------------

    private static (GameState State, int ScoreDelta)? Apply(GameState state, Move move) => move.Type switch
    {
        MoveType.Draw => ApplyDraw(state),
        MoveType.Recycle => ApplyRecycle(state),
        MoveType.WasteToFoundation => ApplyWasteToFoundation(state),
        MoveType.WasteToTableau => ApplyWasteToTableau(state, move),
        MoveType.TableauToTableau => ApplyTableauToTableau(state, move),
        MoveType.TableauToFoundation => ApplyTableauToFoundation(state, move),
        MoveType.FoundationToTableau => ApplyFoundationToTableau(state, move),
        _ => null,
    };

    private static (GameState, int)? ApplyDraw(GameState state)
    {
        if (state.Stock.IsEmpty)
        {
            return null;
        }

        int k = Math.Min(state.Options.DrawCount, state.Stock.Length);
        var drawn = state.Stock.AsSpan(0, k);
        var newStock = state.Stock.RemoveRange(0, k);
        var newWaste = state.Waste.AddRange(drawn);

        return (state.With(stock: newStock, waste: newWaste), 0);
    }

    private static (GameState, int)? ApplyRecycle(GameState state)
    {
        if (!state.Stock.IsEmpty || state.Waste.IsEmpty)
        {
            return null;
        }

        if (state.RedealsUsed >= state.Options.MaxRedeals)
        {
            return null;
        }

        // Flipping the waste pile over restores the original draw order:
        // waste[0] (first drawn) becomes the new stock top.
        var newStock = state.Waste;
        int penalty = state.Options.DrawCount == 1 ? Scoring.RecycleDrawOne : Scoring.RecycleDrawThree;

        return (
            state.With(
                stock: newStock,
                waste: ImmutableArray<Card>.Empty,
                redealsUsed: state.RedealsUsed + 1),
            penalty);
    }

    private static (GameState, int)? ApplyWasteToFoundation(GameState state)
    {
        if (state.WasteTop is not { } card || !CanPlaceOnFoundation(state, card))
        {
            return null;
        }

        var newFoundations = state.Foundations.SetItem((int)card.Suit, card.Rank);
        var newWaste = state.Waste.RemoveAt(state.Waste.Length - 1);

        return (state.With(waste: newWaste, foundations: newFoundations), Scoring.WasteToFoundation);
    }

    private static (GameState, int)? ApplyWasteToTableau(GameState state, Move move)
    {
        if (!IsTableauIndex(move.Destination) || state.WasteTop is not { } card)
        {
            return null;
        }

        var dest = state.Tableau[move.Destination];
        if (!CanPlaceOnTableau(dest, card))
        {
            return null;
        }

        var newWaste = state.Waste.RemoveAt(state.Waste.Length - 1);
        var newTableau = state.Tableau.SetItem(move.Destination, dest.Append([card]));

        return (state.With(waste: newWaste, tableau: newTableau), Scoring.WasteToTableau);
    }

    private static (GameState, int)? ApplyTableauToFoundation(GameState state, Move move)
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

        var newSource = source.RemoveTop(1, out bool flipped);
        var newFoundations = state.Foundations.SetItem((int)card.Suit, card.Rank);
        var newTableau = state.Tableau.SetItem(move.Source, newSource);

        int delta = Scoring.TableauToFoundation + (flipped ? Scoring.TurnOverTableauCard : 0);
        return (state.With(tableau: newTableau, foundations: newFoundations), delta);
    }

    private static (GameState, int)? ApplyFoundationToTableau(GameState state, Move move)
    {
        if (move.Source is < 0 or >= GameState.FoundationCount || !IsTableauIndex(move.Destination))
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

        return (state.With(tableau: newTableau, foundations: newFoundations), Scoring.FoundationToTableau);
    }

    private static (GameState, int)? ApplyTableauToTableau(GameState state, Move move)
    {
        if (!IsTableauIndex(move.Source) || !IsTableauIndex(move.Destination) || move.Source == move.Destination)
        {
            return null;
        }

        var source = state.Tableau[move.Source];
        if (move.Count < 1 || move.Count > source.FaceUpCount)
        {
            return null;
        }

        var moved = source.Cards.AsSpan(source.Count - move.Count, move.Count);
        if (!IsValidRun(moved))
        {
            return null;
        }

        var dest = state.Tableau[move.Destination];
        if (!CanPlaceOnTableau(dest, moved[0]))
        {
            return null;
        }

        var newSource = source.RemoveTop(move.Count, out bool flipped);
        var newDest = dest.Append(moved);
        var newTableau = state.Tableau
            .SetItem(move.Source, newSource)
            .SetItem(move.Destination, newDest);

        int delta = flipped ? Scoring.TurnOverTableauCard : 0;
        return (state.With(tableau: newTableau), delta);
    }

    private static bool IsTableauIndex(int index) => index is >= 0 and < GameState.TableauCount;
}
