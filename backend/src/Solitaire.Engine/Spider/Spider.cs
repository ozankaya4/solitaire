using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// The authoritative, pure Spider engine. Mirrors the <see cref="Klondike"/>
/// architecture and move-application contract: deal, validate/apply, legal-move
/// generation, win detection, and deterministic replay. No method mutates inputs.
/// </summary>
public static class Spider
{
    /// <summary>Cards dealt per stock deal (one to each tableau pile).</summary>
    public const int DealWidth = SpiderState.TableauCount;

    /// <summary>Length of a full suit sequence (King down to Ace).</summary>
    public const int SequenceLength = 13;

    /// <summary>Creates the initial game state for a seed and rule set.</summary>
    public static SpiderState NewGame(int seed, SpiderOptions options)
    {
        options.Validate();

        var shuffled = SpiderDeck.Shuffle(seed, options.SuitCount);
        var (tableau, stock) = SpiderDeck.Deal(shuffled);

        return new SpiderState(
            options,
            stock: stock,
            tableau: tableau,
            completedSequences: 0,
            score: SpiderScoring.InitialScore);
    }

    /// <summary>True once all eight suit sequences are complete.</summary>
    public static bool IsWin(SpiderState state) => state.IsWon;

    /// <summary>
    /// Validates and applies a move. On success returns true with the resulting
    /// <paramref name="next"/> state and nominal <paramref name="scoreDelta"/>. On
    /// failure returns false, leaves <paramref name="next"/> as the input state,
    /// and makes no changes.
    /// </summary>
    public static bool TryApplyMove(SpiderState state, SpiderMove move, out SpiderState next, out int scoreDelta)
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
        next = partial.With(score: SpiderScoring.Clamp(state.Score + delta));
        return true;
    }

    /// <summary>Returns every legal move from <paramref name="state"/> (used for hints).</summary>
    /// <remarks>
    /// As in Klondike, relocating an entire face-up-only column onto an empty column
    /// is omitted (it makes no progress); it is still accepted by <see cref="TryApplyMove"/>.
    /// </remarks>
    public static IReadOnlyList<SpiderMove> GetLegalMoves(SpiderState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var moves = new List<SpiderMove>();

        if (CanDeal(state))
        {
            moves.Add(SpiderMove.Deal());
        }

        for (int s = 0; s < SpiderState.TableauCount; s++)
        {
            var source = state.Tableau[s];
            int runLength = InSuitRunLength(source);
            for (int count = 1; count <= runLength; count++)
            {
                var bottom = source.Cards[source.Count - count];
                for (int d = 0; d < SpiderState.TableauCount; d++)
                {
                    if (d == s)
                    {
                        continue;
                    }

                    var dest = state.Tableau[d];
                    if (dest.IsEmpty)
                    {
                        if (source.FaceDownCount == 0 && count == source.Count)
                        {
                            continue; // futile whole-column relocation
                        }

                        moves.Add(SpiderMove.TableauToTableau(s, d, count));
                    }
                    else if (dest.Cards[^1].Rank == bottom.Rank + 1)
                    {
                        moves.Add(SpiderMove.TableauToTableau(s, d, count));
                    }
                }
            }
        }

        return moves;
    }

    /// <summary>Deterministically replays a move sequence from a fresh game.</summary>
    public static ReplayResult<SpiderState> Replay(int seed, SpiderOptions options, IReadOnlyList<SpiderMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var state = NewGame(seed, options);

        for (int i = 0; i < moves.Count; i++)
        {
            if (!TryApplyMove(state, moves[i], out var nextState, out _))
            {
                return new ReplayResult<SpiderState>(state, state.Score, false, false, i);
            }

            state = nextState;
        }

        return new ReplayResult<SpiderState>(state, state.Score, state.IsWon, true, null);
    }

    // ----------------------------------------------------------------------
    // Portable move (de)serialization for vectors and the API.
    // ----------------------------------------------------------------------

    /// <summary>Converts a Spider move to its portable <see cref="MoveDto"/> form.</summary>
    public static MoveDto ToDto(SpiderMove move) => move.Type switch
    {
        SpiderMoveType.Deal => new MoveDto("Deal"),
        SpiderMoveType.TableauToTableau =>
            new MoveDto("TableauToTableau", move.Source, move.Destination, move.Count),
        _ => throw new ArgumentOutOfRangeException(nameof(move), move.Type, "Unknown move type."),
    };

    /// <summary>Parses a portable <see cref="MoveDto"/> into a Spider move.</summary>
    public static SpiderMove FromDto(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return dto.Type switch
        {
            "Deal" => SpiderMove.Deal(),
            "TableauToTableau" => SpiderMove.TableauToTableau(
                Req(dto.Source, "source"), Req(dto.Destination, "destination"), Req(dto.Count, "count")),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.Type, "Unknown move type."),
        };
    }

    private static int Req(int? value, string field) =>
        value ?? throw new ArgumentException($"Spider move is missing '{field}'.");

    /// <summary>Reads Spider options from a portable options bag.</summary>
    public static SpiderOptions OptionsFromBag(IReadOnlyDictionary<string, int> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.TryGetValue("suitCount", out int suitCount))
        {
            throw new ArgumentException("Spider options require 'suitCount'.");
        }

        var result = new SpiderOptions(suitCount);
        result.Validate();
        return result;
    }

    // ----------------------------------------------------------------------
    // Rule helpers
    // ----------------------------------------------------------------------

    private static bool CanDeal(SpiderState state)
    {
        if (state.Stock.Length < DealWidth)
        {
            return false;
        }

        // Spider forbids dealing while any column is empty.
        foreach (var pile in state.Tableau)
        {
            if (pile.IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Length of the same-suit, descending-by-one run at the top of a pile.</summary>
    private static int InSuitRunLength(TableauPile pile)
    {
        if (pile.FaceUpCount == 0)
        {
            return 0;
        }

        int length = 1;
        for (int i = pile.Count - 1; i > pile.FaceDownCount; i--)
        {
            Card upper = pile.Cards[i];
            Card lower = pile.Cards[i - 1];
            if (lower.Suit == upper.Suit && lower.Rank == upper.Rank + 1)
            {
                length++;
            }
            else
            {
                break;
            }
        }

        return length;
    }

    /// <summary>
    /// Repeatedly removes a completed King→Ace same-suit sequence from the top of a
    /// pile (flipping any newly exposed card). Returns the updated pile and how many
    /// sequences were completed.
    /// </summary>
    private static (TableauPile Pile, int Completed) CompleteSequences(TableauPile pile)
    {
        int completed = 0;
        while (IsCompletableTop(pile))
        {
            pile = pile.RemoveTop(SequenceLength, out _);
            completed++;
        }

        return (pile, completed);
    }

    private static bool IsCompletableTop(TableauPile pile)
    {
        if (pile.FaceUpCount < SequenceLength)
        {
            return false;
        }

        int start = pile.Count - SequenceLength;
        Suit suit = pile.Cards[start].Suit;
        for (int i = 0; i < SequenceLength; i++)
        {
            Card card = pile.Cards[start + i];
            if (card.Suit != suit || card.Rank != SequenceLength - i)
            {
                return false;
            }
        }

        return true;
    }

    // ----------------------------------------------------------------------
    // Move application. Returns null on any rule violation. Never mutates input.
    // ----------------------------------------------------------------------

    private static (SpiderState State, int ScoreDelta)? Apply(SpiderState state, SpiderMove move) => move.Type switch
    {
        SpiderMoveType.Deal => ApplyDeal(state),
        SpiderMoveType.TableauToTableau => ApplyTableauToTableau(state, move),
        _ => null,
    };

    private static (SpiderState, int)? ApplyDeal(SpiderState state)
    {
        if (!CanDeal(state))
        {
            return null;
        }

        var tableau = state.Tableau.ToBuilder();
        int completed = 0;
        for (int p = 0; p < SpiderState.TableauCount; p++)
        {
            var withCard = tableau[p].Append([state.Stock[p]]);
            var (pile, done) = CompleteSequences(withCard);
            tableau[p] = pile;
            completed += done;
        }

        var newStock = state.Stock.RemoveRange(0, DealWidth);
        int delta = SpiderScoring.MovePenalty + (completed * SpiderScoring.CompletedSequenceBonus);

        return (
            state.With(
                stock: newStock,
                tableau: tableau.ToImmutable(),
                completedSequences: state.CompletedSequences + completed),
            delta);
    }

    private static (SpiderState, int)? ApplyTableauToTableau(SpiderState state, SpiderMove move)
    {
        if (!IsTableauIndex(move.Source) || !IsTableauIndex(move.Destination) || move.Source == move.Destination)
        {
            return null;
        }

        var source = state.Tableau[move.Source];
        if (move.Count < 1 || move.Count > InSuitRunLength(source))
        {
            return null;
        }

        var moved = source.Cards.AsSpan(source.Count - move.Count, move.Count);
        var dest = state.Tableau[move.Destination];

        // A run may land on an empty column or on a card exactly one rank higher
        // (suit does not matter for placement, only for moving the run together).
        if (!dest.IsEmpty && dest.Cards[^1].Rank != moved[0].Rank + 1)
        {
            return null;
        }

        var newSource = source.RemoveTop(move.Count, out _);
        var (newDest, completed) = CompleteSequences(dest.Append(moved));

        var newTableau = state.Tableau
            .SetItem(move.Source, newSource)
            .SetItem(move.Destination, newDest);

        int delta = SpiderScoring.MovePenalty + (completed * SpiderScoring.CompletedSequenceBonus);
        return (
            state.With(tableau: newTableau, completedSequences: state.CompletedSequences + completed),
            delta);
    }

    private static bool IsTableauIndex(int index) => index is >= 0 and < SpiderState.TableauCount;
}
