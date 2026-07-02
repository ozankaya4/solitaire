// Turns an engine snapshot into a renderable board model: a set of piles, each
// holding render-cards with a stable key (for Motion layout tweens) and face-up
// flag. Card values are shared with the engine (suit/rank).

import type { Card } from '../engine/cards';
import { ordinalIndex } from '../engine/cards';
import type { KlondikeState } from '../engine/klondike';
import type { SpiderState } from '../engine/spider';
import type { AnyState } from './engineAdapter';
import type { VariantId } from '../app/types';

export type PileKind = 'stock' | 'waste' | 'foundation' | 'tableau';

export interface RenderCard {
  /** Stable identity for layout animations (unique for Klondike; occurrence-tagged for Spider). */
  readonly key: string;
  readonly card: Card;
  readonly faceUp: boolean;
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
  /** Spider: completed K→A sequences (0..8). */
  readonly completed?: number;
}

export function pileId(kind: PileKind, index = 0): string {
  return kind === 'tableau' || kind === 'foundation' ? `${kind}-${index}` : kind;
}

export function boardModel(variant: VariantId, state: AnyState): BoardModel {
  return variant === 'spider'
    ? spiderModel(state as SpiderState)
    : klondikeModel(state as KlondikeState);
}

function klondikeModel(state: KlondikeState): BoardModel {
  const faceDownKey = (card: Card): string => `k${ordinalIndex(card)}`;

  const stock: Pile = {
    id: 'stock',
    kind: 'stock',
    index: 0,
    cards: state.stock.map((card) => ({ key: faceDownKey(card), card, faceUp: false })),
  };
  const waste: Pile = {
    id: 'waste',
    kind: 'waste',
    index: 0,
    cards: state.waste.map((card) => ({ key: faceDownKey(card), card, faceUp: true })),
  };
  const foundations: Pile[] = state.foundations.map((topRank, suit) => ({
    id: pileId('foundation', suit),
    kind: 'foundation',
    index: suit,
    cards:
      topRank === 0
        ? []
        : Array.from({ length: topRank }, (_, i) => {
            const card: Card = { suit, rank: i + 1 };
            return { key: faceDownKey(card), card, faceUp: true };
          }),
  }));
  const tableau: Pile[] = state.tableau.map((pile, index) => ({
    id: pileId('tableau', index),
    kind: 'tableau',
    index,
    cards: pile.cards.map((card, pos) => ({
      key: faceDownKey(card),
      card,
      faceUp: pos >= pile.faceDownCount,
    })),
  }));

  return { variant: 'klondike', stock, waste, foundations, tableau };
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
