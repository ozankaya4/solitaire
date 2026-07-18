// TriPeaks engine — a behavioral port of the C# TriPeaks/*.cs files. Same deal,
// same exposure/adjacency rules, same scoring.
//
// Rules (per project decision): redeals are unlimited; the game is won once
// the three peaks (28 tableau cards) are fully cleared (the stock/waste need
// not empty). An exposed tableau card may be played onto the waste when its
// rank is exactly one above or below the current waste-top card's rank, with
// King/Ace wraparound allowed (King <-> Ace counts as adjacent).

import { ACE, KING, type Card } from './cards';
import { DECK_SIZE, shuffle } from './deck';
import type {
  ApplyResult,
  GameDefinition,
  MoveDto,
  ReplayOutcome,
  ReplayResult,
  SolitaireEngine,
} from './types';

export const TRIPEAKS_TABLEAU_SIZE = 28;

const SCORE = {
  playToWaste: 10,
} as const;

/**
 * Each non-base tableau index's two "children" in the row below — the cards it
 * rests on and is covered by. Indices 0-2 are the three peak apexes; 3-8 the
 * next row (2 per peak); 9-17 the row below that (3 per peak); 18-27 the
 * shared 10-card base row, which has no children and is therefore always
 * exposed (entry is undefined).
 */
const CHILDREN: readonly (readonly [number, number] | undefined)[] = [
  [3, 4], [5, 6], [7, 8],
  [9, 10], [10, 11], [12, 13], [13, 14], [15, 16], [16, 17],
  [18, 19], [19, 20], [20, 21], [21, 22], [22, 23], [23, 24], [24, 25], [25, 26], [26, 27],
  undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
];

export interface TriPeaksState {
  readonly tableau: readonly (Card | undefined)[]; // length 28, see CHILDREN; undefined = removed
  readonly stock: readonly Card[]; // index 0 = top (next drawn)
  readonly waste: readonly Card[]; // last element = the card to build on
  readonly score: number;
}

export function tripeaksIsWon(state: TriPeaksState): boolean {
  return state.tableau.every((card) => card === undefined);
}

export function tripeaksNewGame(seed: number): TriPeaksState {
  const shuffled = shuffle(seed);
  const tableau = shuffled.slice(0, TRIPEAKS_TABLEAU_SIZE);
  const waste = [shuffled[TRIPEAKS_TABLEAU_SIZE]!];
  const stock = shuffled.slice(TRIPEAKS_TABLEAU_SIZE + 1, DECK_SIZE);
  return { tableau, stock, waste, score: 0 };
}

type Applied = { readonly state: TriPeaksState; readonly delta: number } | null;

export function tripeaksTryApplyMove(
  state: TriPeaksState,
  move: MoveDto,
): ApplyResult<TriPeaksState> {
  const result = apply(state, move);
  if (result === null) {
    return { ok: false, next: state, scoreDelta: 0 };
  }
  return {
    ok: true,
    next: { ...result.state, score: state.score + result.delta },
    scoreDelta: result.delta,
  };
}

export function tripeaksReplay(seed: number, moves: readonly MoveDto[]): ReplayResult<TriPeaksState> {
  let state = tripeaksNewGame(seed);
  for (let i = 0; i < moves.length; i++) {
    const applied = tripeaksTryApplyMove(state, moves[i]!);
    if (!applied.ok) {
      return {
        finalState: state,
        score: state.score,
        won: false,
        allMovesLegal: false,
        firstIllegalMoveIndex: i,
      };
    }
    state = applied.next;
  }
  return {
    finalState: state,
    score: state.score,
    won: tripeaksIsWon(state),
    allMovesLegal: true,
    firstIllegalMoveIndex: null,
  };
}

export function tripeaksGetLegalMoves(state: TriPeaksState): MoveDto[] {
  const moves: MoveDto[] = [];

  if (state.stock.length > 0) {
    moves.push({ type: 'Draw' });
  } else if (state.waste.length > 0) {
    moves.push({ type: 'Recycle' });
  }

  const wasteTop = tripeaksWasteTop(state);
  if (wasteTop !== undefined) {
    for (let i = 0; i < TRIPEAKS_TABLEAU_SIZE; i++) {
      if (isExposed(state, i) && isRankAdjacent(state.tableau[i]!.rank, wasteTop.rank)) {
        moves.push({ type: 'PlayToWaste', source: i });
      }
    }
  }

  return moves;
}

export const tripeaksEngine: SolitaireEngine = {
  variant: 'tripeaks',
  replay(game: GameDefinition): ReplayOutcome {
    const result = tripeaksReplay(game.seed, game.moves);
    return {
      score: result.score,
      won: result.won,
      allMovesLegal: result.allMovesLegal,
      firstIllegalMoveIndex: result.firstIllegalMoveIndex,
    };
  },
};

// -- rule helpers -------------------------------------------------------------

function tripeaksWasteTop(state: TriPeaksState): Card | undefined {
  return state.waste.length === 0 ? undefined : state.waste[state.waste.length - 1];
}

/**
 * True if the tableau slot at `index` holds a card AND both of the cards it
 * rests on in the row below (its "children") are gone — or it has no
 * children (the shared base row), which is always exposed.
 */
function isExposed(state: TriPeaksState, index: number): boolean {
  if (state.tableau[index] === undefined) {
    return false;
  }
  const children = CHILDREN[index];
  if (children === undefined) {
    return true;
  }
  const [a, b] = children;
  return state.tableau[a] === undefined && state.tableau[b] === undefined;
}

/** Adjacent by rank, one step either direction, with King<->Ace wraparound. */
function isRankAdjacent(rankA: number, rankB: number): boolean {
  const diff = Math.abs(rankA - rankB);
  return diff === 1 || diff === KING - ACE;
}

// -- move application (returns null on any rule violation) --------------------

function apply(state: TriPeaksState, move: MoveDto): Applied {
  switch (move.type) {
    case 'Draw':
      return applyDraw(state);
    case 'Recycle':
      return applyRecycle(state);
    case 'PlayToWaste':
      return applyPlayToWaste(state, move.source ?? -1);
    default:
      return null;
  }
}

function applyDraw(state: TriPeaksState): Applied {
  if (state.stock.length === 0) {
    return null;
  }
  const card = state.stock[0]!;
  return {
    state: { ...state, stock: state.stock.slice(1), waste: [...state.waste, card] },
    delta: 0,
  };
}

function applyRecycle(state: TriPeaksState): Applied {
  if (state.stock.length > 0 || state.waste.length === 0) {
    return null;
  }
  // Flipping the waste pile over restores the original draw order: waste[0]
  // (first drawn) becomes the new stock top. The waste is left empty, so a
  // Draw is needed before another card can be played.
  return { state: { ...state, stock: [...state.waste], waste: [] }, delta: 0 };
}

function applyPlayToWaste(state: TriPeaksState, position: number): Applied {
  if (position < 0 || position >= TRIPEAKS_TABLEAU_SIZE || !isExposed(state, position)) {
    return null;
  }
  const wasteTop = tripeaksWasteTop(state);
  if (wasteTop === undefined) {
    return null;
  }
  const card = state.tableau[position]!;
  if (!isRankAdjacent(card.rank, wasteTop.rank)) {
    return null;
  }
  const tableau = [...state.tableau];
  tableau[position] = undefined;
  return {
    state: { ...state, tableau, waste: [...state.waste, card] },
    delta: SCORE.playToWaste,
  };
}
