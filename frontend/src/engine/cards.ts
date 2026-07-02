// Card model — a direct port of Cards.cs. The integer suit values and the
// suit-major ordinal are part of the deterministic contract shared with the C#
// engine and must not change.

export enum Suit {
  Clubs = 0,
  Diamonds = 1,
  Hearts = 2,
  Spades = 3,
}

export const ACE = 1;
export const KING = 13;

export interface Card {
  readonly suit: Suit;
  readonly rank: number; // 1..13 (Ace = 1, King = 13)
}

/** Diamonds and Hearts are red; Clubs and Spades are black. */
export function isRed(suit: Suit): boolean {
  return suit === Suit.Diamonds || suit === Suit.Hearts;
}

/** True if two suits share a color (used for alternating-color rules). */
export function sameColor(a: Suit, b: Suit): boolean {
  return isRed(a) === isRed(b);
}

/** Canonical 0..51 index: suit-major, `suit * 13 + (rank - 1)`. */
export function ordinalIndex(card: Card): number {
  return card.suit * 13 + (card.rank - 1);
}

/** Builds a card from its canonical 0..51 ordinal index. */
export function cardFromOrdinal(index: number): Card {
  return { suit: Math.floor(index / 13), rank: (index % 13) + 1 };
}
