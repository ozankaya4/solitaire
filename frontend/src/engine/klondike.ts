// Klondike engine — a behavioral port of Klondike.cs / Scoring.cs / GameState.cs.
// Same deal, same move legality, same scoring, same clamping.

import { KING, sameColor, type Card } from './cards';
import { dealKlondike, KLONDIKE_TABLEAU_COUNT, shuffle } from './deck';
import {
  appendCards,
  faceUpCount,
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

const FOUNDATION_COUNT = 4;
export const KLONDIKE_UNLIMITED_REDEALS = 2147483647; // int.MaxValue

// Documented scoring model (Scoring.cs).
const SCORE = {
  wasteToFoundation: 10,
  tableauToFoundation: 10,
  wasteToTableau: 5,
  turnOver: 5,
  foundationToTableau: -15,
  recycleDraw1: -100,
  recycleDraw3: -20,
} as const;

const clamp = (score: number): number => (score < 0 ? 0 : score);

export interface KlondikeOptions {
  readonly drawCount: number; // 1 or 3
  readonly maxRedeals: number; // KLONDIKE_UNLIMITED_REDEALS == unlimited
}

export interface KlondikeState {
  readonly options: KlondikeOptions;
  readonly stock: readonly Card[]; // index 0 = top (next drawn)
  readonly waste: readonly Card[]; // last element = playable top
  readonly foundations: readonly number[]; // top rank per suit index (0 = empty)
  readonly tableau: readonly TableauPile[];
  readonly score: number;
  readonly redealsUsed: number;
}

export function klondikeIsWon(state: KlondikeState): boolean {
  return state.foundations.every((top) => top === KING);
}

export function klondikeNewGame(seed: number, options: KlondikeOptions): KlondikeState {
  validateOptions(options);
  const { tableau, stock } = dealKlondike(shuffle(seed));
  return {
    options,
    stock,
    waste: [],
    foundations: [0, 0, 0, 0],
    tableau,
    score: 0,
    redealsUsed: 0,
  };
}

type Applied = { readonly state: KlondikeState; readonly delta: number } | null;

export function klondikeTryApplyMove(
  state: KlondikeState,
  move: MoveDto,
): ApplyResult<KlondikeState> {
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

export function klondikeReplay(
  seed: number,
  options: KlondikeOptions,
  moves: readonly MoveDto[],
): ReplayResult<KlondikeState> {
  let state = klondikeNewGame(seed, options);
  for (let i = 0; i < moves.length; i++) {
    const applied = klondikeTryApplyMove(state, moves[i]!);
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
    won: klondikeIsWon(state),
    allMovesLegal: true,
    firstIllegalMoveIndex: null,
  };
}

export function klondikeGetLegalMoves(state: KlondikeState): MoveDto[] {
  const moves: MoveDto[] = [];

  if (state.stock.length > 0) {
    moves.push({ type: 'Draw' });
  } else if (state.waste.length > 0 && state.redealsUsed < state.options.maxRedeals) {
    moves.push({ type: 'Recycle' });
  }

  const wasteCard = wasteTop(state);
  if (wasteCard !== undefined) {
    if (canPlaceOnFoundation(state, wasteCard)) {
      moves.push({ type: 'WasteToFoundation' });
    }
    for (let t = 0; t < KLONDIKE_TABLEAU_COUNT; t++) {
      if (canPlaceOnTableau(state.tableau[t]!, wasteCard)) {
        moves.push({ type: 'WasteToTableau', destination: t });
      }
    }
  }

  for (let s = 0; s < KLONDIKE_TABLEAU_COUNT; s++) {
    const top = topCard(state.tableau[s]!);
    if (top !== undefined && canPlaceOnFoundation(state, top)) {
      moves.push({ type: 'TableauToFoundation', source: s });
    }
  }

  for (let f = 0; f < FOUNDATION_COUNT; f++) {
    const rank = state.foundations[f]!;
    if (rank === 0) {
      continue;
    }
    const card: Card = { suit: f, rank };
    for (let t = 0; t < KLONDIKE_TABLEAU_COUNT; t++) {
      if (canPlaceOnTableau(state.tableau[t]!, card)) {
        moves.push({ type: 'FoundationToTableau', source: f, destination: t });
      }
    }
  }

  for (let s = 0; s < KLONDIKE_TABLEAU_COUNT; s++) {
    const source = state.tableau[s]!;
    const up = faceUpCount(source);
    for (let count = 1; count <= up; count++) {
      const bottom = source.cards[source.cards.length - count]!;
      for (let t = 0; t < KLONDIKE_TABLEAU_COUNT; t++) {
        if (t === s) {
          continue;
        }
        const dest = state.tableau[t]!;
        if (isPileEmpty(dest) && source.faceDownCount === 0 && count === up) {
          continue; // futile whole-column relocation
        }
        if (canPlaceOnTableau(dest, bottom)) {
          moves.push({ type: 'TableauToTableau', source: s, destination: t, count });
        }
      }
    }
  }

  return moves;
}

export function klondikeOptionsFromBag(bag: Readonly<Record<string, number>>): KlondikeOptions {
  const drawCount = bag['drawCount'];
  if (drawCount === undefined) {
    throw new Error("Klondike options require 'drawCount'.");
  }
  const maxRedeals = bag['maxRedeals'] ?? KLONDIKE_UNLIMITED_REDEALS;
  const options = { drawCount, maxRedeals };
  validateOptions(options);
  return options;
}

export const klondikeEngine: SolitaireEngine = {
  variant: 'klondike',
  replay(game: GameDefinition): ReplayOutcome {
    const options = klondikeOptionsFromBag(game.options);
    const result = klondikeReplay(game.seed, options, game.moves);
    return {
      score: result.score,
      won: result.won,
      allMovesLegal: result.allMovesLegal,
      firstIllegalMoveIndex: result.firstIllegalMoveIndex,
    };
  },
};

// -- rule helpers -------------------------------------------------------------

function validateOptions(options: KlondikeOptions): void {
  if (options.drawCount !== 1 && options.drawCount !== 3) {
    throw new Error('DrawCount must be 1 or 3.');
  }
  if (options.maxRedeals < 0) {
    throw new Error('MaxRedeals must be >= 0.');
  }
}

function wasteTop(state: KlondikeState): Card | undefined {
  return state.waste.length === 0 ? undefined : state.waste[state.waste.length - 1];
}

function canPlaceOnFoundation(state: KlondikeState, moving: Card): boolean {
  return state.foundations[moving.suit] === moving.rank - 1;
}

function canPlaceOnTableau(pile: TableauPile, moving: Card): boolean {
  if (isPileEmpty(pile)) {
    return moving.rank === KING;
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

function isTableauIndex(index: number): boolean {
  return index >= 0 && index < KLONDIKE_TABLEAU_COUNT;
}

// -- move application (returns null on any rule violation) --------------------

function apply(state: KlondikeState, move: MoveDto): Applied {
  switch (move.type) {
    case 'Draw':
      return applyDraw(state);
    case 'Recycle':
      return applyRecycle(state);
    case 'WasteToFoundation':
      return applyWasteToFoundation(state);
    case 'WasteToTableau':
      return applyWasteToTableau(state, move.destination ?? -1);
    case 'TableauToFoundation':
      return applyTableauToFoundation(state, move.source ?? -1);
    case 'FoundationToTableau':
      return applyFoundationToTableau(state, move.source ?? -1, move.destination ?? -1);
    case 'TableauToTableau':
      return applyTableauToTableau(
        state,
        move.source ?? -1,
        move.destination ?? -1,
        move.count ?? 0,
      );
    default:
      return null;
  }
}

function applyDraw(state: KlondikeState): Applied {
  if (state.stock.length === 0) {
    return null;
  }
  const k = Math.min(state.options.drawCount, state.stock.length);
  const drawn = state.stock.slice(0, k);
  return {
    state: { ...state, stock: state.stock.slice(k), waste: [...state.waste, ...drawn] },
    delta: 0,
  };
}

function applyRecycle(state: KlondikeState): Applied {
  if (state.stock.length > 0 || state.waste.length === 0) {
    return null;
  }
  if (state.redealsUsed >= state.options.maxRedeals) {
    return null;
  }
  // Flipping the waste over restores the original draw order: waste[0] (first
  // drawn) becomes the new stock top.
  const penalty = state.options.drawCount === 1 ? SCORE.recycleDraw1 : SCORE.recycleDraw3;
  return {
    state: { ...state, stock: [...state.waste], waste: [], redealsUsed: state.redealsUsed + 1 },
    delta: penalty,
  };
}

function applyWasteToFoundation(state: KlondikeState): Applied {
  const card = wasteTop(state);
  if (card === undefined || !canPlaceOnFoundation(state, card)) {
    return null;
  }
  const foundations = [...state.foundations];
  foundations[card.suit] = card.rank;
  return {
    state: { ...state, foundations, waste: state.waste.slice(0, -1) },
    delta: SCORE.wasteToFoundation,
  };
}

function applyWasteToTableau(state: KlondikeState, destination: number): Applied {
  const card = wasteTop(state);
  if (!isTableauIndex(destination) || card === undefined) {
    return null;
  }
  const dest = state.tableau[destination]!;
  if (!canPlaceOnTableau(dest, card)) {
    return null;
  }
  const tableau = [...state.tableau];
  tableau[destination] = appendCards(dest, [card]);
  return {
    state: { ...state, waste: state.waste.slice(0, -1), tableau },
    delta: SCORE.wasteToTableau,
  };
}

function applyTableauToFoundation(state: KlondikeState, source: number): Applied {
  if (!isTableauIndex(source)) {
    return null;
  }
  const pile = state.tableau[source]!;
  const card = topCard(pile);
  if (card === undefined || !canPlaceOnFoundation(state, card)) {
    return null;
  }
  const { pile: newSource, flipped } = removeTop(pile, 1);
  const foundations = [...state.foundations];
  foundations[card.suit] = card.rank;
  const tableau = [...state.tableau];
  tableau[source] = newSource;
  return {
    state: { ...state, foundations, tableau },
    delta: SCORE.tableauToFoundation + (flipped ? SCORE.turnOver : 0),
  };
}

function applyFoundationToTableau(
  state: KlondikeState,
  source: number,
  destination: number,
): Applied {
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
  return { state: { ...state, foundations, tableau }, delta: SCORE.foundationToTableau };
}

function applyTableauToTableau(
  state: KlondikeState,
  source: number,
  destination: number,
  count: number,
): Applied {
  if (!isTableauIndex(source) || !isTableauIndex(destination) || source === destination) {
    return null;
  }
  const src = state.tableau[source]!;
  if (count < 1 || count > faceUpCount(src)) {
    return null;
  }
  const moved = src.cards.slice(src.cards.length - count);
  if (!isValidRun(moved)) {
    return null;
  }
  const dest = state.tableau[destination]!;
  if (!canPlaceOnTableau(dest, moved[0]!)) {
    return null;
  }
  const { pile: newSource, flipped } = removeTop(src, count);
  const tableau = [...state.tableau];
  tableau[source] = newSource;
  tableau[destination] = appendCards(dest, moved);
  return { state: { ...state, tableau }, delta: flipped ? SCORE.turnOver : 0 };
}
