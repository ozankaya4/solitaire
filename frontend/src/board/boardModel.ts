// Turns an engine snapshot into a renderable board model: a set of piles, each
// holding render-cards with a stable key (for move animations) and face-up flag.
// Card values are shared with the engine (suit/rank).
//
// Klondike foundations are displayed through a SLOT mapping: the engine keys
// foundations by suit, but visually an ace may be placed on any empty top slot,
// which then hosts that suit. `foundationSlots[i]` is the suit assigned to slot i
// (or null → an empty "fslot-i" placeholder that accepts any ace).

import type { Card } from '../engine/cards';
import { ordinalIndex } from '../engine/cards';
import type { FreeCellState } from '../engine/freecell';
import type { KlondikeState } from '../engine/klondike';
import { PYRAMID_ROW_COUNT, type PyramidState } from '../engine/pyramid';
import type { SpiderState } from '../engine/spider';
import type { TriPeaksState } from '../engine/tripeaks';
import type { AnyState } from './engineAdapter';
import type { VariantId } from '../app/types';

export type PileKind = 'stock' | 'waste' | 'foundation' | 'tableau' | 'freecell' | 'pyramid' | 'tripeaks';

export interface RenderCard {
  /** Stable identity for move animations (unique for Klondike; occurrence-tagged for Spider). */
  readonly key: string;
  readonly card: Card;
  readonly faceUp: boolean;
  /** Pyramid/TriPeaks only: true once both cards it rests on are gone (or it's base-row). */
  readonly exposed?: boolean;
}

export interface Pile {
  readonly id: string;
  readonly kind: PileKind;
  readonly index: number;
  readonly cards: readonly RenderCard[];
}

export interface BoardModel {
  readonly variant: VariantId;
  readonly stock?: Pile;
  readonly waste?: Pile;
  readonly foundations: readonly Pile[];
  readonly tableau: readonly Pile[];
  /** FreeCell: the four holding cells (each 0 or 1 cards). */
  readonly freeCells?: readonly Pile[];
  /** Pyramid: the 28 triangle slots, flat row-major (each 0 or 1 cards). */
  readonly pyramid?: readonly Pile[];
  /** TriPeaks: the 28 tableau slots (three peaks + shared base row). */
  readonly tripeaks?: readonly Pile[];
  /** Spider: completed K→A sequences (0..8). */
  readonly completed?: number;
  /** Klondike: cards flipped per draw (drives the waste fan). */
  readonly drawCount?: number;
}

export type FoundationSlots = readonly (number | null)[];

export const UNASSIGNED_SLOTS: FoundationSlots = [null, null, null, null];

export function pileId(kind: PileKind, index = 0): string {
  return kind === 'tableau' ||
    kind === 'foundation' ||
    kind === 'freecell' ||
    kind === 'pyramid' ||
    kind === 'tripeaks'
    ? `${kind}-${index}`
    : kind;
}

export function boardModel(
  variant: VariantId,
  state: AnyState,
  foundationSlots: FoundationSlots = [0, 1, 2, 3],
): BoardModel {
  switch (variant) {
    case 'spider':
      return spiderModel(state as SpiderState);
    case 'freecell':
      return freecellModel(state as FreeCellState, foundationSlots);
    case 'pyramid':
      return pyramidModel(state as PyramidState);
    case 'tripeaks':
      return tripeaksModel(state as TriPeaksState);
    default:
      return klondikeModel(state as KlondikeState, foundationSlots);
  }
}

// Foundations in display-slot order: an assigned slot shows its suit's pile (id
// stays foundation-{suit} so hints/moves address it by suit); an unassigned
// slot is an empty placeholder that accepts any ace. Shared by every variant
// whose foundations are suit-keyed with 0..13 ranks (Klondike, FreeCell).
function foundationPiles(
  foundationRanks: readonly number[],
  slots: FoundationSlots,
  cardKey: (card: Card) => string,
): Pile[] {
  return slots.map((suit, slot) => {
    if (suit === null) {
      return { id: `fslot-${slot}`, kind: 'foundation', index: slot, cards: [] };
    }
    const topRank = foundationRanks[suit] ?? 0;
    return {
      id: pileId('foundation', suit),
      kind: 'foundation',
      index: suit,
      cards: Array.from({ length: topRank }, (_, i) => {
        const card: Card = { suit, rank: i + 1 };
        return { key: cardKey(card), card, faceUp: true };
      }),
    };
  });
}

function klondikeModel(state: KlondikeState, slots: FoundationSlots): BoardModel {
  const cardKey = (card: Card): string => `k${ordinalIndex(card)}`;

  const stock: Pile = {
    id: 'stock',
    kind: 'stock',
    index: 0,
    cards: state.stock.map((card) => ({ key: cardKey(card), card, faceUp: false })),
  };
  const waste: Pile = {
    id: 'waste',
    kind: 'waste',
    index: 0,
    cards: state.waste.map((card) => ({ key: cardKey(card), card, faceUp: true })),
  };

  const foundations = foundationPiles(state.foundations, slots, cardKey);

  const tableau: Pile[] = state.tableau.map((pile, index) => ({
    id: pileId('tableau', index),
    kind: 'tableau',
    index,
    cards: pile.cards.map((card, pos) => ({
      key: cardKey(card),
      card,
      faceUp: pos >= pile.faceDownCount,
    })),
  }));

  return {
    variant: 'klondike',
    stock,
    waste,
    foundations,
    tableau,
    drawCount: state.options.drawCount,
  };
}

function spiderModel(state: SpiderState): BoardModel {
  // Spider has duplicate cards, so tag each key with its occurrence order.
  const seen = new Map<number, number>();
  const key = (card: Card): string => {
    const ord = ordinalIndex(card);
    const n = seen.get(ord) ?? 0;
    seen.set(ord, n + 1);
    return `s${ord}#${n}`;
  };

  const stock: Pile = {
    id: 'stock',
    kind: 'stock',
    index: 0,
    cards: state.stock.map((card) => ({ key: key(card), card, faceUp: false })),
  };
  const tableau: Pile[] = state.tableau.map((pile, index) => ({
    id: pileId('tableau', index),
    kind: 'tableau',
    index,
    cards: pile.cards.map((card, pos) => ({
      key: key(card),
      card,
      faceUp: pos >= pile.faceDownCount,
    })),
  }));

  return {
    variant: 'spider',
    stock,
    foundations: [],
    tableau,
    completed: state.completedSequences,
  };
}

function freecellModel(state: FreeCellState, slots: FoundationSlots): BoardModel {
  const cardKey = (card: Card): string => `f${ordinalIndex(card)}`;

  const foundations = foundationPiles(state.foundations, slots, cardKey);

  const freeCells: Pile[] = state.freeCells.map((card, index) => ({
    id: pileId('freecell', index),
    kind: 'freecell',
    index,
    cards: card ? [{ key: cardKey(card), card, faceUp: true }] : [],
  }));

  const tableau: Pile[] = state.tableau.map((pile, index) => ({
    id: pileId('tableau', index),
    kind: 'tableau',
    index,
    cards: pile.cards.map((card) => ({ key: cardKey(card), card, faceUp: true })),
  }));

  return {
    variant: 'freecell',
    foundations,
    freeCells,
    tableau,
  };
}

// Mirrors Pyramid.cs's/pyramid.ts's exposure math: a slot is exposed once both
// cards it rests on (its row+1 "children") are gone, or it's in the base row.
function flatIndex(row: number, col: number): number {
  return (row * (row + 1)) / 2 + col;
}

function rowOf(index: number): number {
  let row = 0;
  while (flatIndex(row + 1, 0) <= index) {
    row++;
  }
  return row;
}

function isSlotExposed(pyramid: readonly (Card | undefined)[], index: number): boolean {
  if (pyramid[index] === undefined) {
    return false;
  }
  const row = rowOf(index);
  if (row === PYRAMID_ROW_COUNT - 1) {
    return true;
  }
  const col = index - flatIndex(row, 0);
  return (
    pyramid[flatIndex(row + 1, col)] === undefined && pyramid[flatIndex(row + 1, col + 1)] === undefined
  );
}

function pyramidModel(state: PyramidState): BoardModel {
  const cardKey = (card: Card): string => `p${ordinalIndex(card)}`;

  const pyramid: Pile[] = state.pyramid.map((card, index) => ({
    id: pileId('pyramid', index),
    kind: 'pyramid',
    index,
    cards: card
      ? [{ key: cardKey(card), card, faceUp: true, exposed: isSlotExposed(state.pyramid, index) }]
      : [],
  }));

  const stock: Pile = {
    id: 'stock',
    kind: 'stock',
    index: 0,
    cards: state.stock.map((card) => ({ key: cardKey(card), card, faceUp: false })),
  };
  const waste: Pile = {
    id: 'waste',
    kind: 'waste',
    index: 0,
    cards: state.waste.map((card) => ({ key: cardKey(card), card, faceUp: true })),
  };

  return {
    variant: 'pyramid',
    stock,
    waste,
    foundations: [],
    tableau: [],
    pyramid,
  };
}

// Mirrors TriPeaks.cs's/tripeaks.ts's children table: a slot is exposed once
// both cards it rests on in the row below are gone, or it's in the shared
// base row (no children).
const TRIPEAKS_CHILDREN: readonly (readonly [number, number] | undefined)[] = [
  [3, 4], [5, 6], [7, 8],
  [9, 10], [10, 11], [12, 13], [13, 14], [15, 16], [16, 17],
  [18, 19], [19, 20], [20, 21], [21, 22], [22, 23], [23, 24], [24, 25], [25, 26], [26, 27],
  undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
];

function isTriPeaksSlotExposed(tableau: readonly (Card | undefined)[], index: number): boolean {
  if (tableau[index] === undefined) {
    return false;
  }
  const children = TRIPEAKS_CHILDREN[index];
  if (children === undefined) {
    return true;
  }
  const [a, b] = children;
  return tableau[a] === undefined && tableau[b] === undefined;
}

function tripeaksModel(state: TriPeaksState): BoardModel {
  const cardKey = (card: Card): string => `t${ordinalIndex(card)}`;

  const tripeaks: Pile[] = state.tableau.map((card, index) => ({
    id: pileId('tripeaks', index),
    kind: 'tripeaks',
    index,
    cards: card
      ? [{ key: cardKey(card), card, faceUp: true, exposed: isTriPeaksSlotExposed(state.tableau, index) }]
      : [],
  }));

  const stock: Pile = {
    id: 'stock',
    kind: 'stock',
    index: 0,
    cards: state.stock.map((card) => ({ key: cardKey(card), card, faceUp: false })),
  };
  const waste: Pile = {
    id: 'waste',
    kind: 'waste',
    index: 0,
    cards: state.waste.map((card) => ({ key: cardKey(card), card, faceUp: true })),
  };

  return {
    variant: 'tripeaks',
    stock,
    waste,
    foundations: [],
    tableau: [],
    tripeaks,
  };
}
