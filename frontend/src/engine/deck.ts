// Deck construction, the shared seeded shuffle, and the Klondike deal —
// port of Deck.cs. Reproduces the documented Fisher–Yates pass exactly.

import { cardFromOrdinal, type Card } from './cards';
import { DeterministicRandom } from './random';
import type { TableauPile } from './tableau';

export const DECK_SIZE = 52;
export const KLONDIKE_TABLEAU_COUNT = 7;

/** The canonical unshuffled 52-card deck (ordinal 0..51). */
export function buildOrdered(): Card[] {
  const cards: Card[] = [];
  for (let i = 0; i < DECK_SIZE; i++) {
    cards.push(cardFromOrdinal(i));
  }
  return cards;
}

/**
 * The shared seeded Fisher–Yates (Durstenfeld) shuffle used by every variant.
 * For i from length-1 down to 1, swap items[i] with items[rng.nextInt(i + 1)].
 */
export function shuffleInPlace(items: Card[], rng: DeterministicRandom): void {
  for (let i = items.length - 1; i >= 1; i--) {
    const j = rng.nextInt(i + 1);
    const tmp = items[i]!;
    items[i] = items[j]!;
    items[j] = tmp;
  }
}

/** Returns the ordered 52-card deck shuffled with the seeded Fisher–Yates pass. */
export function shuffle(seed: number): Card[] {
  const cards = buildOrdered();
  shuffleInPlace(cards, new DeterministicRandom(seed));
  return cards;
}

/**
 * Deals a shuffled deck into the initial Klondike layout: column c (0..6) gets
 * c+1 cards, the last dealt face-up; the remaining 24 cards form the stock
 * (index 0 = top).
 */
export function dealKlondike(shuffled: readonly Card[]): {
  tableau: TableauPile[];
  stock: Card[];
} {
  const tableau: TableauPile[] = [];
  let next = 0;
  for (let c = 0; c < KLONDIKE_TABLEAU_COUNT; c++) {
    const cardsInColumn = c + 1;
    const column: Card[] = [];
    for (let r = 0; r < cardsInColumn; r++) {
      column.push(shuffled[next++]!);
    }
    tableau.push({ cards: column, faceDownCount: cardsInColumn - 1 });
  }
  return { tableau, stock: shuffled.slice(next) };
}
