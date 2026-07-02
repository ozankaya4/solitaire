// Spider engine — a behavioral port of Spider.cs / SpiderDeck.cs /
// SpiderScoring.cs / SpiderState.cs. Same deal, move legality, auto-completion,
// and scoring across 1-, 2- and 4-suit difficulty.

import type { Card, Suit } from './cards';
import { shuffleInPlace } from './deck';
import { DeterministicRandom } from './random';
import { appendCards, faceUpCount, isPileEmpty, removeTop, type TableauPile } from './tableau';
import type {
  ApplyResult,
  GameDefinition,
  MoveDto,
  ReplayOutcome,
  ReplayResult,
  SolitaireEngine,
} from './types';

export const SPIDER_DECK_SIZE = 104;
export const SPIDER_TABLEAU_COUNT = 10;
export const SPIDER_TOTAL_SEQUENCES = 8;
const DEAL_WIDTH = SPIDER_TABLEAU_COUNT;
const SEQUENCE_LENGTH = 13;

// Documented scoring model (SpiderScoring.cs).
const INITIAL_SCORE = 500;
const MOVE_PENALTY = -1;
const COMPLETED_SEQUENCE_BONUS = 100;

const clamp = (score: number): number => (score < 0 ? 0 : score);

export interface SpiderOptions {
  readonly suitCount: number; // 1, 2, or 4
}

export interface SpiderState {
  readonly options: SpiderOptions;
  readonly stock: readonly Card[]; // index 0 = next dealt
  readonly tableau: readonly TableauPile[];
  readonly completedSequences: number; // 0..8
  readonly score: number;
}

export function spiderIsWon(state: SpiderState): boolean {
  return state.completedSequences === SPIDER_TOTAL_SEQUENCES;
}

/** Canonical unshuffled 104-card Spider deck for a suit count (copy-major, suit, rank). */
export function buildSpiderDeck(suitCount: number): Card[] {
  const suits = suitsFor(suitCount);
  const copies = SPIDER_DECK_SIZE / (13 * suitCount);
  const cards: Card[] = [];
  for (let copy = 0; copy < copies; copy++) {
    for (const suit of suits) {
      for (let rank = 1; rank <= 13; rank++) {
        cards.push({ suit, rank });
      }
    }
  }
  return cards;
}

export function shuffleSpider(seed: number, suitCount: number): Card[] {
  const cards = buildSpiderDeck(suitCount);
  shuffleInPlace(cards, new DeterministicRandom(seed));
  return cards;
}

function dealSpider(shuffled: readonly Card[]): { tableau: TableauPile[]; stock: Card[] } {
  const tableau: TableauPile[] = [];
  let next = 0;
  for (let c = 0; c < SPIDER_TABLEAU_COUNT; c++) {
    const n = c < 4 ? 6 : 5;
    const column: Card[] = [];
    for (let r = 0; r < n; r++) {
      column.push(shuffled[next++]!);
    }
    tableau.push({ cards: column, faceDownCount: n - 1 });
  }
  return { tableau, stock: shuffled.slice(next) };
}

export function spiderNewGame(seed: number, options: SpiderOptions): SpiderState {
  validateOptions(options);
  const { tableau, stock } = dealSpider(shuffleSpider(seed, options.suitCount));
  return { options, stock, tableau, completedSequences: 0, score: INITIAL_SCORE };
}

type Applied = { readonly state: SpiderState; readonly delta: number } | null;

export function spiderTryApplyMove(state: SpiderState, move: MoveDto): ApplyResult<SpiderState> {
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

export function spiderReplay(
  seed: number,
  options: SpiderOptions,
  moves: readonly MoveDto[],
): ReplayResult<SpiderState> {
  let state = spiderNewGame(seed, options);
  for (let i = 0; i < moves.length; i++) {
    const applied = spiderTryApplyMove(state, moves[i]!);
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
    won: spiderIsWon(state),
    allMovesLegal: true,
    firstIllegalMoveIndex: null,
  };
}

export function spiderGetLegalMoves(state: SpiderState): MoveDto[] {
  const moves: MoveDto[] = [];

  if (canDeal(state)) {
    moves.push({ type: 'Deal' });
  }

  for (let s = 0; s < SPIDER_TABLEAU_COUNT; s++) {
    const source = state.tableau[s]!;
    const runLength = inSuitRunLength(source);
    for (let count = 1; count <= runLength; count++) {
      const bottom = source.cards[source.cards.length - count]!;
      for (let d = 0; d < SPIDER_TABLEAU_COUNT; d++) {
        if (d === s) {
          continue;
        }
        const dest = state.tableau[d]!;
        if (isPileEmpty(dest)) {
          if (source.faceDownCount === 0 && count === source.cards.length) {
            continue; // futile whole-column relocation
          }
          moves.push({ type: 'TableauToTableau', source: s, destination: d, count });
        } else if (dest.cards[dest.cards.length - 1]!.rank === bottom.rank + 1) {
          moves.push({ type: 'TableauToTableau', source: s, destination: d, count });
        }
      }
    }
  }

  return moves;
}

export function spiderOptionsFromBag(bag: Readonly<Record<string, number>>): SpiderOptions {
  const suitCount = bag['suitCount'];
  if (suitCount === undefined) {
    throw new Error("Spider options require 'suitCount'.");
  }
  const options = { suitCount };
  validateOptions(options);
  return options;
}

export const spiderEngine: SolitaireEngine = {
  variant: 'spider',
  replay(game: GameDefinition): ReplayOutcome {
    const options = spiderOptionsFromBag(game.options);
    const result = spiderReplay(game.seed, options, game.moves);
    return {
      score: result.score,
      won: result.won,
      allMovesLegal: result.allMovesLegal,
      firstIllegalMoveIndex: result.firstIllegalMoveIndex,
    };
  },
};

// -- rule helpers -------------------------------------------------------------

function suitsFor(suitCount: number): Suit[] {
  switch (suitCount) {
    case 1:
      return [3]; // Spades
    case 2:
      return [3, 2]; // Spades, Hearts
    case 4:
      return [0, 1, 2, 3] as Suit[]; // Clubs, Diamonds, Hearts, Spades
    default:
      throw new RangeError('SuitCount must be 1, 2, or 4.');
  }
}

function validateOptions(options: SpiderOptions): void {
  if (options.suitCount !== 1 && options.suitCount !== 2 && options.suitCount !== 4) {
    throw new RangeError('Spider SuitCount must be 1, 2, or 4.');
  }
}

function canDeal(state: SpiderState): boolean {
  if (state.stock.length < DEAL_WIDTH) {
    return false;
  }
  return state.tableau.every((pile) => !isPileEmpty(pile));
}

/** Length of the same-suit, descending-by-one run at the top of a pile. */
function inSuitRunLength(pile: TableauPile): number {
  if (faceUpCount(pile) === 0) {
    return 0;
  }
  let length = 1;
  for (let i = pile.cards.length - 1; i > pile.faceDownCount; i--) {
    const upper = pile.cards[i]!;
    const lower = pile.cards[i - 1]!;
    if (lower.suit === upper.suit && lower.rank === upper.rank + 1) {
      length++;
    } else {
      break;
    }
  }
  return length;
}

function isCompletableTop(pile: TableauPile): boolean {
  if (faceUpCount(pile) < SEQUENCE_LENGTH) {
    return false;
  }
  const start = pile.cards.length - SEQUENCE_LENGTH;
  const suit = pile.cards[start]!.suit;
  for (let i = 0; i < SEQUENCE_LENGTH; i++) {
    const card = pile.cards[start + i]!;
    if (card.suit !== suit || card.rank !== SEQUENCE_LENGTH - i) {
      return false;
    }
  }
  return true;
}

/** Repeatedly removes a completed King→Ace run from a pile; returns the count removed. */
function completeSequences(pile: TableauPile): { pile: TableauPile; completed: number } {
  let current = pile;
  let completed = 0;
  while (isCompletableTop(current)) {
    current = removeTop(current, SEQUENCE_LENGTH).pile;
    completed++;
  }
  return { pile: current, completed };
}

function isTableauIndex(index: number): boolean {
  return index >= 0 && index < SPIDER_TABLEAU_COUNT;
}

// -- move application (returns null on any rule violation) --------------------

function apply(state: SpiderState, move: MoveDto): Applied {
  switch (move.type) {
    case 'Deal':
      return applyDeal(state);
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

function applyDeal(state: SpiderState): Applied {
  if (!canDeal(state)) {
    return null;
  }
  const tableau = [...state.tableau];
  let completed = 0;
  for (let p = 0; p < SPIDER_TABLEAU_COUNT; p++) {
    const withCard = appendCards(tableau[p]!, [state.stock[p]!]);
    const result = completeSequences(withCard);
    tableau[p] = result.pile;
    completed += result.completed;
  }
  return {
    state: {
      ...state,
      stock: state.stock.slice(DEAL_WIDTH),
      tableau,
      completedSequences: state.completedSequences + completed,
    },
    delta: MOVE_PENALTY + completed * COMPLETED_SEQUENCE_BONUS,
  };
}

function applyTableauToTableau(
  state: SpiderState,
  source: number,
  destination: number,
  count: number,
): Applied {
  if (!isTableauIndex(source) || !isTableauIndex(destination) || source === destination) {
    return null;
  }
  const src = state.tableau[source]!;
  if (count < 1 || count > inSuitRunLength(src)) {
    return null;
  }
  const moved = src.cards.slice(src.cards.length - count);
  const dest = state.tableau[destination]!;

  // A run may land on an empty column or on a card exactly one rank higher
  // (suit does not matter for placement, only for moving the run together).
  if (!isPileEmpty(dest) && dest.cards[dest.cards.length - 1]!.rank !== moved[0]!.rank + 1) {
    return null;
  }

  const newSource = removeTop(src, count).pile;
  const { pile: newDest, completed } = completeSequences(appendCards(dest, moved));
  const tableau = [...state.tableau];
  tableau[source] = newSource;
  tableau[destination] = newDest;
  return {
    state: { ...state, tableau, completedSequences: state.completedSequences + completed },
    delta: MOVE_PENALTY + completed * COMPLETED_SEQUENCE_BONUS,
  };
}
