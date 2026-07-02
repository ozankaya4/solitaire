// Tableau pile model and shared operations — port of TableauPile.cs. Cards are
// stored bottom-to-top; face-down cards are always a contiguous block at the
// bottom, described by faceDownCount.

import type { Card } from './cards';

export interface TableauPile {
  readonly cards: readonly Card[]; // bottom (index 0) -> top (last)
  readonly faceDownCount: number;
}

export const emptyPile: TableauPile = { cards: [], faceDownCount: 0 };

export function pileCount(pile: TableauPile): number {
  return pile.cards.length;
}

export function faceUpCount(pile: TableauPile): number {
  return pile.cards.length - pile.faceDownCount;
}

export function isPileEmpty(pile: TableauPile): boolean {
  return pile.cards.length === 0;
}

export function topCard(pile: TableauPile): Card | undefined {
  return pile.cards.length === 0 ? undefined : pile.cards[pile.cards.length - 1];
}

export interface RemoveTopResult {
  readonly pile: TableauPile;
  readonly flipped: boolean;
}

/**
 * Removes the top `count` cards. If that empties the face-up run while face-down
 * cards remain, the newly exposed card is turned face-up (`flipped = true`).
 */
export function removeTop(pile: TableauPile, count: number): RemoveTopResult {
  const newCount = pile.cards.length - count;
  const cards = pile.cards.slice(0, newCount);
  let faceDownCount = pile.faceDownCount;
  let flipped = false;

  if (newCount > 0 && faceDownCount === newCount) {
    faceDownCount -= 1;
    flipped = true;
  }

  return { pile: { cards, faceDownCount }, flipped };
}

/** Returns a copy with `cards` pushed on top, face-up. */
export function appendCards(pile: TableauPile, cards: readonly Card[]): TableauPile {
  return { cards: [...pile.cards, ...cards], faceDownCount: pile.faceDownCount };
}
