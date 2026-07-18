// Pyramid engine — a behavioral port of the C# Pyramid/*.cs files. Same deal,
// same exposure/pairing rules, same scoring.
//
// Rules (per project decision): redeals are unlimited; the game is won once
// the 28-card triangle is fully cleared (the stock/waste need not empty).
// Pairs summing to 13 are removed together; a lone King (rank 13) is removed
// by itself. Only the pyramid's currently-exposed cards and the single waste
// top card are ever eligible — the stock is always face-down/inaccessible.

import { KING, type Card } from './cards';
import { DECK_SIZE, shuffle } from './deck';
import type {
  ApplyResult,
  GameDefinition,
  MoveDto,
  ReplayOutcome,
  ReplayResult,
  SolitaireEngine,
} from './types';

export const PYRAMID_ROW_COUNT = 7;
export const PYRAMID_SIZE = 28; // 1+2+...+7

/** Sentinel position meaning "the waste's top card" rather than a pyramid slot. */
export const PYRAMID_WASTE = -1;

const SCORE = {
  removePair: 15,
  removeSingle: 10,
} as const;

export interface PyramidState {
  readonly pyramid: readonly (Card | undefined)[]; // length 28, flat row-major; undefined = removed
  readonly stock: readonly Card[]; // index 0 = top (next drawn)
  readonly waste: readonly Card[]; // last element = playable top
  readonly score: number;
}

export function pyramidIsWon(state: PyramidState): boolean {
  return state.pyramid.every((card) => card === undefined);
}

export function pyramidNewGame(seed: number): PyramidState {
  const shuffled = shuffle(seed);
  const pyramid: (Card | undefined)[] = [];
  let next = 0;
  for (let row = 0; row < PYRAMID_ROW_COUNT; row++) {
    for (let col = 0; col <= row; col++) {
      pyramid.push(shuffled[next++]);
    }
  }
  const stock = shuffled.slice(next, DECK_SIZE);
  return { pyramid, stock, waste: [], score: 0 };
}

type Applied = { readonly state: PyramidState; readonly delta: number } | null;

export function pyramidTryApplyMove(
  state: PyramidState,
  move: MoveDto,
): ApplyResult<PyramidState> {
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

export function pyramidReplay(seed: number, moves: readonly MoveDto[]): ReplayResult<PyramidState> {
  let state = pyramidNewGame(seed);
  for (let i = 0; i < moves.length; i++) {
    const applied = pyramidTryApplyMove(state, moves[i]!);
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
    won: pyramidIsWon(state),
    allMovesLegal: true,
    firstIllegalMoveIndex: null,
  };
}

export function pyramidGetLegalMoves(state: PyramidState): MoveDto[] {
  const moves: MoveDto[] = [];

  if (state.stock.length > 0) {
    moves.push({ type: 'Draw' });
  } else if (state.waste.length > 0) {
    moves.push({ type: 'Recycle' });
  }

  const exposed: number[] = [];
  for (let i = 0; i < PYRAMID_SIZE; i++) {
    if (isExposed(state, i)) {
      exposed.push(i);
    }
  }

  const wasteTop = pyramidWasteTop(state);

  for (const i of exposed) {
    if (state.pyramid[i]!.rank === KING) {
      moves.push({ type: 'RemoveSingle', source: i });
    }
  }
  if (wasteTop !== undefined && wasteTop.rank === KING) {
    moves.push({ type: 'RemoveSingle', source: PYRAMID_WASTE });
  }

  for (let a = 0; a < exposed.length; a++) {
    const rankA = state.pyramid[exposed[a]!]!.rank;
    if (rankA === KING) {
      continue;
    }
    for (let b = a + 1; b < exposed.length; b++) {
      const rankB = state.pyramid[exposed[b]!]!.rank;
      if (rankA + rankB === 13) {
        moves.push({ type: 'RemovePair', source: exposed[a]!, destination: exposed[b]! });
      }
    }
  }

  if (wasteTop !== undefined && wasteTop.rank !== KING) {
    for (const i of exposed) {
      const rank = state.pyramid[i]!.rank;
      if (rank !== KING && rank + wasteTop.rank === 13) {
        moves.push({ type: 'RemovePair', source: i, destination: PYRAMID_WASTE });
      }
    }
  }

  return moves;
}

export const pyramidEngine: SolitaireEngine = {
  variant: 'pyramid',
  replay(game: GameDefinition): ReplayOutcome {
    const result = pyramidReplay(game.seed, game.moves);
    return {
      score: result.score,
      won: result.won,
      allMovesLegal: result.allMovesLegal,
      firstIllegalMoveIndex: result.firstIllegalMoveIndex,
    };
  },
};

// -- rule helpers -------------------------------------------------------------

function pyramidWasteTop(state: PyramidState): Card | undefined {
  return state.waste.length === 0 ? undefined : state.waste[state.waste.length - 1];
}

/** Flat index of row `row`, column `col` (both 0-based). */
function flatIndex(row: number, col: number): number {
  return (row * (row + 1)) / 2 + col;
}

/** The row (0..6) a flat pyramid index belongs to. */
function rowOf(index: number): number {
  let row = 0;
  while (flatIndex(row + 1, 0) <= index) {
    row++;
  }
  return row;
}

/**
 * True if the pyramid slot at `index` holds a card AND both of the cards it
 * rests on in the row below (its "children") are gone — or it is in the base
 * row, which has nothing beneath it.
 */
function isExposed(state: PyramidState, index: number): boolean {
  if (state.pyramid[index] === undefined) {
    return false;
  }
  const row = rowOf(index);
  if (row === PYRAMID_ROW_COUNT - 1) {
    return true;
  }
  const col = index - flatIndex(row, 0);
  const childLeft = flatIndex(row + 1, col);
  const childRight = flatIndex(row + 1, col + 1);
  return state.pyramid[childLeft] === undefined && state.pyramid[childRight] === undefined;
}

/** Resolves a move position (pyramid slot or PYRAMID_WASTE) to a card, if exposed/available. */
function resolveExposedCard(state: PyramidState, position: number): Card | undefined {
  if (position === PYRAMID_WASTE) {
    return pyramidWasteTop(state);
  }
  if (position < 0 || position >= PYRAMID_SIZE) {
    return undefined;
  }
  return isExposed(state, position) ? state.pyramid[position] : undefined;
}

/** Removes the card at `position` from wherever it lives. */
function removeAt(state: PyramidState, position: number): PyramidState {
  if (position === PYRAMID_WASTE) {
    return { ...state, waste: state.waste.slice(0, -1) };
  }
  const pyramid = [...state.pyramid];
  pyramid[position] = undefined;
  return { ...state, pyramid };
}

// -- move application (returns null on any rule violation) --------------------

function apply(state: PyramidState, move: MoveDto): Applied {
  switch (move.type) {
    case 'Draw':
      return applyDraw(state);
    case 'Recycle':
      return applyRecycle(state);
    case 'RemoveSingle':
      return applyRemoveSingle(state, move.source ?? -2);
    case 'RemovePair':
      return applyRemovePair(state, move.source ?? -2, move.destination ?? -2);
    default:
      return null;
  }
}

function applyDraw(state: PyramidState): Applied {
  if (state.stock.length === 0) {
    return null;
  }
  const card = state.stock[0]!;
  return {
    state: { ...state, stock: state.stock.slice(1), waste: [...state.waste, card] },
    delta: 0,
  };
}

function applyRecycle(state: PyramidState): Applied {
  if (state.stock.length > 0 || state.waste.length === 0) {
    return null;
  }
  // Flipping the waste pile over restores the original draw order: waste[0]
  // (first drawn) becomes the new stock top.
  return { state: { ...state, stock: [...state.waste], waste: [] }, delta: 0 };
}

function applyRemoveSingle(state: PyramidState, position: number): Applied {
  const card = resolveExposedCard(state, position);
  if (card === undefined || card.rank !== KING) {
    return null;
  }
  return { state: removeAt(state, position), delta: SCORE.removeSingle };
}

function applyRemovePair(state: PyramidState, positionA: number, positionB: number): Applied {
  if (positionA === positionB) {
    return null;
  }
  const a = resolveExposedCard(state, positionA);
  const b = resolveExposedCard(state, positionB);
  if (a === undefined || b === undefined || a.rank === KING || b.rank === KING) {
    return null;
  }
  if (a.rank + b.rank !== 13) {
    return null;
  }
  const afterA = removeAt(state, positionA);
  const afterBoth = removeAt(afterA, positionB);
  return { state: afterBoth, delta: SCORE.removePair };
}
