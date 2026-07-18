// FreeCell engine — a behavioral port of the C# FreeCell/*.cs files. Same deal,
// same move legality (including the "any card on an empty column" rule that
// differs from Klondike, and the standard supermove formula), same scoring.

import { KING, sameColor, type Card } from './cards';
import { DECK_SIZE, shuffle } from './deck';
import {
  appendCards,
  isPileEmpty,
  removeTop,
  topCard,
  type TableauPile,
} from './tableau';
import type {
  ApplyResult,
  GameDefinition,
  MoveDto,
  ReplayOutcome,
  ReplayResult,
  SolitaireEngine,
} from './types';

export const FREECELL_TABLEAU_COUNT = 8;
export const FREECELL_FREE_CELL_COUNT = 4;
const FOUNDATION_COUNT = 4;

// Columns 0..3 get 7 cards; columns 4..7 get 6 cards (28 + 24 = 52).
const COLUMN_SIZES = [7, 7, 7, 7, 6, 6, 6, 6] as const;

// Documented scoring model (FreeCellScoring.cs) — simpler than Klondike's:
// every tableau card is already face-up from the deal (no turn-over bonus is
// possible), and there is no stock/waste/redeal.
const SCORE = {
  toFoundation: 10,
  foundationToTableau: -15,
} as const;

const clamp = (score: number): number => (score < 0 ? 0 : score);

export interface FreeCellState {
  readonly tableau: readonly TableauPile[]; // length 8; every card is face-up
  readonly freeCells: readonly (Card | undefined)[]; // length 4
  readonly foundations: readonly number[]; // top rank per suit index (0 = empty)
  readonly score: number;
}

export function freecellIsWon(state: FreeCellState): boolean {
  return state.foundations.every((top) => top === KING);
}

export function freecellNewGame(seed: number): FreeCellState {
  const shuffled = shuffle(seed);
  if (shuffled.length !== DECK_SIZE) {
    throw new Error('A full 52-card deck is required.');
  }
  const tableau: TableauPile[] = [];
  let next = 0;
  for (const size of COLUMN_SIZES) {
    const cards: Card[] = [];
    for (let i = 0; i < size; i++) {
      cards.push(shuffled[next++]!);
    }
    // Every card is dealt face-up: faceDownCount is always 0.
    tableau.push({ cards, faceDownCount: 0 });
  }
  return {
    tableau,
    freeCells: [undefined, undefined, undefined, undefined],
    foundations: [0, 0, 0, 0],
    score: 0,
  };
}

type Applied = { readonly state: FreeCellState; readonly delta: number } | null;

export function freecellTryApplyMove(
  state: FreeCellState,
  move: MoveDto,
): ApplyResult<FreeCellState> {
  const result = apply(state, move);
  if (result === null) {
    return { ok: false, next: state, scoreDelta: 0 };
  }
  return {
    ok: true,
    next: { ...result.state, score: clamp(state.score + result.delta) },
    scoreDelta: result.delta,
  };
}

export function freecellReplay(
  seed: number,
  moves: readonly MoveDto[],
): ReplayResult<FreeCellState> {
  let state = freecellNewGame(seed);
  for (let i = 0; i < moves.length; i++) {
    const applied = freecellTryApplyMove(state, moves[i]!);
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
    won: freecellIsWon(state),
    allMovesLegal: true,
    firstIllegalMoveIndex: null,
  };
}

export function freecellGetLegalMoves(state: FreeCellState): MoveDto[] {
  const moves: MoveDto[] = [];

  for (let s = 0; s < FREECELL_TABLEAU_COUNT; s++) {
    const top = topCard(state.tableau[s]!);
    if (top !== undefined && canPlaceOnFoundation(state, top)) {
      moves.push({ type: 'TableauToFoundation', source: s });
    }
  }

  for (let c = 0; c < FREECELL_FREE_CELL_COUNT; c++) {
    const card = state.freeCells[c];
    if (card !== undefined && canPlaceOnFoundation(state, card)) {
      moves.push({ type: 'FreeCellToFoundation', source: c });
    }
  }

  const firstEmptyCell = state.freeCells.indexOf(undefined);
  if (firstEmptyCell !== -1) {
    for (let s = 0; s < FREECELL_TABLEAU_COUNT; s++) {
      if (!isPileEmpty(state.tableau[s]!)) {
        moves.push({ type: 'TableauToFreeCell', source: s, destination: firstEmptyCell });
      }
    }
  }

  for (let c = 0; c < FREECELL_FREE_CELL_COUNT; c++) {
    const card = state.freeCells[c];
    if (card === undefined) {
      continue;
    }
    for (let t = 0; t < FREECELL_TABLEAU_COUNT; t++) {
      if (canPlaceOnTableau(state.tableau[t]!, card)) {
        moves.push({ type: 'FreeCellToTableau', source: c, destination: t });
      }
    }
  }

  for (let f = 0; f < FOUNDATION_COUNT; f++) {
    const rank = state.foundations[f]!;
    if (rank === 0) {
      continue;
    }
    const card: Card = { suit: f, rank };
    for (let t = 0; t < FREECELL_TABLEAU_COUNT; t++) {
      if (canPlaceOnTableau(state.tableau[t]!, card)) {
        moves.push({ type: 'FoundationToTableau', source: f, destination: t });
      }
    }
  }

  for (let s = 0; s < FREECELL_TABLEAU_COUNT; s++) {
    const source = state.tableau[s]!;
    for (let count = 1; count <= source.cards.length; count++) {
      const bottom = source.cards[source.cards.length - count]!;
      for (let t = 0; t < FREECELL_TABLEAU_COUNT; t++) {
        if (t === s) {
          continue;
        }
        const dest = state.tableau[t]!;
        // Relocating an entire column onto another empty column never helps
        // (every FreeCell card is already face-up; nothing is revealed).
        if (isPileEmpty(dest) && count === source.cards.length) {
          continue;
        }
        if (canPlaceOnTableau(dest, bottom) && count <= maxMovableCount(state, t)) {
          moves.push({ type: 'TableauToTableau', source: s, destination: t, count });
        }
      }
    }
  }

  return moves;
}

export const freecellEngine: SolitaireEngine = {
  variant: 'freecell',
  replay(game: GameDefinition): ReplayOutcome {
    const result = freecellReplay(game.seed, game.moves);
    return {
      score: result.score,
      won: result.won,
      allMovesLegal: result.allMovesLegal,
      firstIllegalMoveIndex: result.firstIllegalMoveIndex,
    };
  },
};

// -- rule helpers -------------------------------------------------------------

function canPlaceOnFoundation(state: FreeCellState, moving: Card): boolean {
  return state.foundations[moving.suit] === moving.rank - 1;
}

/**
 * Can `moving` be placed on `pile`? Any card on an empty pile (unlike
 * Klondike); otherwise one rank below the top card and of the opposite color.
 */
function canPlaceOnTableau(pile: TableauPile, moving: Card): boolean {
  if (isPileEmpty(pile)) {
    return true;
  }
  const top = pile.cards[pile.cards.length - 1]!;
  return moving.rank === top.rank - 1 && !sameColor(moving.suit, top.suit);
}

function isValidRun(run: readonly Card[]): boolean {
  for (let i = 1; i < run.length; i++) {
    const lower = run[i]!;
    const upper = run[i - 1]!;
    if (lower.rank !== upper.rank - 1 || sameColor(lower.suit, upper.suit)) {
      return false;
    }
  }
  return true;
}

/**
 * The largest run that can legally move to `destinationIndex` right now:
 * `(1 + emptyFreeCells) * 2^emptyColumns`, where the destination column itself
 * (even if empty) is excluded from the empty-column count — it cannot serve as
 * scratch space for its own incoming run. This is the standard FreeCell
 * "supermove" convention: exactly equivalent to relaying the run one card at a
 * time through the currently available free cells and empty columns.
 */
function maxMovableCount(state: FreeCellState, destinationIndex: number): number {
  const emptyFreeCells = state.freeCells.filter((c) => c === undefined).length;
  let emptyColumns = 0;
  for (let t = 0; t < FREECELL_TABLEAU_COUNT; t++) {
    if (t !== destinationIndex && isPileEmpty(state.tableau[t]!)) {
      emptyColumns++;
    }
  }
  return (1 + emptyFreeCells) * 2 ** emptyColumns;
}

function isTableauIndex(index: number): boolean {
  return index >= 0 && index < FREECELL_TABLEAU_COUNT;
}

function isFreeCellIndex(index: number): boolean {
  return index >= 0 && index < FREECELL_FREE_CELL_COUNT;
}

// -- move application (returns null on any rule violation) --------------------

function apply(state: FreeCellState, move: MoveDto): Applied {
  switch (move.type) {
    case 'TableauToTableau':
      return applyTableauToTableau(
        state,
        move.source ?? -1,
        move.destination ?? -1,
        move.count ?? 0,
      );
    case 'TableauToFreeCell':
      return applyTableauToFreeCell(state, move.source ?? -1, move.destination ?? -1);
    case 'TableauToFoundation':
      return applyTableauToFoundation(state, move.source ?? -1);
    case 'FreeCellToTableau':
      return applyFreeCellToTableau(state, move.source ?? -1, move.destination ?? -1);
    case 'FreeCellToFoundation':
      return applyFreeCellToFoundation(state, move.source ?? -1);
    case 'FoundationToTableau':
      return applyFoundationToTableau(state, move.source ?? -1, move.destination ?? -1);
    default:
      return null;
  }
}

function applyTableauToTableau(
  state: FreeCellState,
  source: number,
  destination: number,
  count: number,
): Applied {
  if (!isTableauIndex(source) || !isTableauIndex(destination) || source === destination) {
    return null;
  }
  const src = state.tableau[source]!;
  if (count < 1 || count > src.cards.length) {
    return null;
  }
  const moved = src.cards.slice(src.cards.length - count);
  if (!isValidRun(moved)) {
    return null;
  }
  const dest = state.tableau[destination]!;
  if (!canPlaceOnTableau(dest, moved[0]!) || count > maxMovableCount(state, destination)) {
    return null;
  }
  const { pile: newSource } = removeTop(src, count); // no flips: nothing is ever face-down
  const tableau = [...state.tableau];
  tableau[source] = newSource;
  tableau[destination] = appendCards(dest, moved);
  return { state: { ...state, tableau }, delta: 0 };
}

function applyTableauToFreeCell(state: FreeCellState, source: number, destination: number): Applied {
  if (!isTableauIndex(source) || !isFreeCellIndex(destination)) {
    return null;
  }
  const src = state.tableau[source]!;
  const card = topCard(src);
  if (card === undefined || state.freeCells[destination] !== undefined) {
    return null;
  }
  const { pile: newSource } = removeTop(src, 1);
  const tableau = [...state.tableau];
  tableau[source] = newSource;
  const freeCells = [...state.freeCells];
  freeCells[destination] = card;
  return { state: { ...state, tableau, freeCells }, delta: 0 };
}

function applyTableauToFoundation(state: FreeCellState, source: number): Applied {
  if (!isTableauIndex(source)) {
    return null;
  }
  const pile = state.tableau[source]!;
  const card = topCard(pile);
  if (card === undefined || !canPlaceOnFoundation(state, card)) {
    return null;
  }
  const { pile: newSource } = removeTop(pile, 1);
  const foundations = [...state.foundations];
  foundations[card.suit] = card.rank;
  const tableau = [...state.tableau];
  tableau[source] = newSource;
  return { state: { ...state, tableau, foundations }, delta: SCORE.toFoundation };
}

function applyFreeCellToTableau(state: FreeCellState, source: number, destination: number): Applied {
  if (!isFreeCellIndex(source) || !isTableauIndex(destination)) {
    return null;
  }
  const card = state.freeCells[source];
  if (card === undefined) {
    return null;
  }
  const dest = state.tableau[destination]!;
  if (!canPlaceOnTableau(dest, card)) {
    return null;
  }
  const tableau = [...state.tableau];
  tableau[destination] = appendCards(dest, [card]);
  const freeCells = [...state.freeCells];
  freeCells[source] = undefined;
  return { state: { ...state, tableau, freeCells }, delta: 0 };
}

function applyFreeCellToFoundation(state: FreeCellState, source: number): Applied {
  if (!isFreeCellIndex(source)) {
    return null;
  }
  const card = state.freeCells[source];
  if (card === undefined || !canPlaceOnFoundation(state, card)) {
    return null;
  }
  const foundations = [...state.foundations];
  foundations[card.suit] = card.rank;
  const freeCells = [...state.freeCells];
  freeCells[source] = undefined;
  return { state: { ...state, freeCells, foundations }, delta: SCORE.toFoundation };
}

function applyFoundationToTableau(state: FreeCellState, source: number, destination: number): Applied {
  if (source < 0 || source >= FOUNDATION_COUNT || !isTableauIndex(destination)) {
    return null;
  }
  const rank = state.foundations[source]!;
  if (rank === 0) {
    return null;
  }
  const card: Card = { suit: source, rank };
  const dest = state.tableau[destination]!;
  if (!canPlaceOnTableau(dest, card)) {
    return null;
  }
  const foundations = [...state.foundations];
  foundations[source] = rank - 1;
  const tableau = [...state.tableau];
  tableau[destination] = appendCards(dest, [card]);
  return { state: { ...state, tableau, foundations }, delta: SCORE.foundationToTableau };
}
