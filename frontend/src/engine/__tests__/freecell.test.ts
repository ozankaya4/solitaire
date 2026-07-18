// Focused unit tests for the FreeCell TS port: the deal shape and the rules
// that differ from Klondike (any card on an empty column, the supermove
// formula) — the parts most likely to drift when porting from C#. Broad
// cross-language parity is covered by vectors.test.ts.

import { describe, expect, it } from 'vitest';
import type { Card } from '../cards';
import {
  freecellGetLegalMoves,
  freecellIsWon,
  freecellNewGame,
  freecellTryApplyMove,
  FREECELL_FREE_CELL_COUNT,
  FREECELL_TABLEAU_COUNT,
  type FreeCellState,
} from '../freecell';
import type { TableauPile } from '../tableau';

const C = (suit: number, rank: number): Card => ({ suit, rank });
const pile = (...cards: Card[]): TableauPile => ({ cards, faceDownCount: 0 });
const filler = (): TableauPile => pile(C(0, 10));

function state(overrides: {
  tableau?: TableauPile[];
  freeCells?: (Card | undefined)[];
  foundations?: number[];
  score?: number;
}): FreeCellState {
  const tableau = Array.from(
    { length: FREECELL_TABLEAU_COUNT },
    (_, i) => overrides.tableau?.[i] ?? { cards: [], faceDownCount: 0 },
  );
  const freeCells = Array.from(
    { length: FREECELL_FREE_CELL_COUNT },
    (_, i) => overrides.freeCells?.[i],
  );
  return {
    tableau,
    freeCells,
    foundations: overrides.foundations ?? [0, 0, 0, 0],
    score: overrides.score ?? 0,
  };
}

describe('FreeCell deal', () => {
  it('deals 52 face-up cards into 4 sevens + 4 sixes, no duplicates', () => {
    const s = freecellNewGame(1);
    expect(s.tableau).toHaveLength(8);
    const sizes = s.tableau.map((p) => p.cards.length);
    expect(sizes).toEqual([7, 7, 7, 7, 6, 6, 6, 6]);
    expect(s.tableau.every((p) => p.faceDownCount === 0)).toBe(true);

    const seen = new Set<number>();
    for (const p of s.tableau) {
      for (const c of p.cards) {
        const ord = c.suit * 13 + (c.rank - 1);
        expect(seen.has(ord)).toBe(false);
        seen.add(ord);
      }
    }
    expect(seen.size).toBe(52);
  });

  it('is deterministic for a seed and differs across seeds', () => {
    expect(freecellNewGame(42)).toEqual(freecellNewGame(42));
    expect(freecellNewGame(1).tableau[0]).not.toEqual(freecellNewGame(2).tableau[0]);
  });
});

describe('FreeCell tableau rules', () => {
  it('allows ANY rank on an empty column (unlike Klondike)', () => {
    const s = state({ tableau: [pile(C(2, 5))] }); // Hearts 5
    const result = freecellTryApplyMove(s, {
      type: 'TableauToTableau',
      source: 0,
      destination: 1,
      count: 1,
    });
    expect(result.ok).toBe(true);
  });

  it('rejects same-color or wrong-rank placement', () => {
    const sameColor = state({ tableau: [pile(C(2, 5)), pile(C(1, 6))] }); // H5 -> D6 (both red)
    expect(
      freecellTryApplyMove(sameColor, { type: 'TableauToTableau', source: 0, destination: 1, count: 1 })
        .ok,
    ).toBe(false);

    const wrongRank = state({ tableau: [pile(C(2, 5)), pile(C(3, 9))] }); // H5 -> S9
    expect(
      freecellTryApplyMove(wrongRank, { type: 'TableauToTableau', source: 0, destination: 1, count: 1 })
        .ok,
    ).toBe(false);
  });

  it('supermove: fits exactly at (1+emptyFreeCells) * 2^emptyColumns, not beyond', () => {
    // 3-card run S8,H7,C6 (bottom->top); dest D9 accepts the run's bottom (S8).
    const run = pile(C(3, 8), C(2, 7), C(0, 6));
    const tableau = [run, pile(C(1, 9)), filler(), filler(), filler(), filler(), filler(), filler()];

    // 2 free cells empty -> max = (1+2)*2^0 = 3: exactly fits.
    const fits = state({ tableau, freeCells: [C(2, 1), C(0, 2), undefined, undefined] });
    expect(
      freecellTryApplyMove(fits, { type: 'TableauToTableau', source: 0, destination: 1, count: 3 }).ok,
    ).toBe(true);

    // 0 free cells empty -> max = 1: the 3-card move is illegal, though a 1-card
    // move (the run's own top, C6, onto a fitting D7 placement) still works.
    const full = C(1, 2);
    const tooFew: TableauPile[] = [...tableau];
    tooFew[2] = pile(C(1, 7)); // D7 accepts C6 (top of the run)
    const blocked = state({ tableau: tooFew, freeCells: [full, full, full, full] });
    expect(
      freecellTryApplyMove(blocked, { type: 'TableauToTableau', source: 0, destination: 1, count: 3 })
        .ok,
    ).toBe(false);
    expect(
      freecellTryApplyMove(blocked, { type: 'TableauToTableau', source: 0, destination: 2, count: 1 })
        .ok,
    ).toBe(true);
  });

  it('the destination column itself does not count toward the empty-column bonus', () => {
    const run = pile(C(3, 8), C(2, 7), C(0, 6));
    const tableau = [run, { cards: [], faceDownCount: 0 }, filler(), filler(), filler(), filler(), filler(), filler()];
    const full = C(1, 2);
    // 1 free cell empty, destination is the ONLY empty column: max = (1+1)*2^0 = 2.
    const s = state({ tableau, freeCells: [undefined, full, full, full] });
    expect(
      freecellTryApplyMove(s, { type: 'TableauToTableau', source: 0, destination: 1, count: 3 }).ok,
    ).toBe(false);
    expect(
      freecellTryApplyMove(s, { type: 'TableauToTableau', source: 0, destination: 1, count: 2 }).ok,
    ).toBe(true);
  });
});

describe('FreeCell free cells', () => {
  it('parks a tableau top card in an empty free cell, and only an empty one', () => {
    const s = state({ tableau: [pile(C(2, 5))] });
    const result = freecellTryApplyMove(s, { type: 'TableauToFreeCell', source: 0, destination: 2 });
    expect(result.ok).toBe(true);
    expect(result.next.freeCells[2]).toEqual(C(2, 5));
    expect(result.next.tableau[0]!.cards).toHaveLength(0);

    const occupied = state({ tableau: [pile(C(2, 5))], freeCells: [C(0, 2)] });
    expect(
      freecellTryApplyMove(occupied, { type: 'TableauToFreeCell', source: 0, destination: 0 }).ok,
    ).toBe(false);
  });

  it('plays a free cell card onto a fitting tableau pile, including an empty one', () => {
    const s = state({ tableau: [pile(C(3, 6))], freeCells: [C(2, 5)] }); // H5 onto S6
    const result = freecellTryApplyMove(s, { type: 'FreeCellToTableau', source: 0, destination: 0 });
    expect(result.ok).toBe(true);
    expect(result.next.freeCells[0]).toBeUndefined();

    const ontoEmpty = state({ freeCells: [C(2, 5)] });
    expect(
      freecellTryApplyMove(ontoEmpty, { type: 'FreeCellToTableau', source: 0, destination: 0 }).ok,
    ).toBe(true);
  });
});

describe('FreeCell foundations', () => {
  it('accepts an ace, rejects an out-of-order rank, and scores +10', () => {
    const ace = state({ tableau: [pile(C(2, 1))] });
    const result = freecellTryApplyMove(ace, { type: 'TableauToFoundation', source: 0 });
    expect(result.ok).toBe(true);
    expect(result.scoreDelta).toBe(10);
    expect(result.next.foundations[2]).toBe(1);

    const outOfOrder = state({ tableau: [pile(C(2, 3))] });
    expect(freecellTryApplyMove(outOfOrder, { type: 'TableauToFoundation', source: 0 }).ok).toBe(false);
  });

  it('sends a foundation card back to an empty (or fitting) tableau column, scoring -15', () => {
    const s = state({ foundations: [0, 0, 3, 0] });
    const result = freecellTryApplyMove(s, {
      type: 'FoundationToTableau',
      source: 2,
      destination: 0,
    });
    expect(result.ok).toBe(true);
    expect(result.scoreDelta).toBe(-15);
    expect(result.next.foundations[2]).toBe(2);
  });
});

describe('FreeCell win detection', () => {
  it('is won only when every foundation reaches King', () => {
    expect(freecellIsWon(state({ foundations: [13, 13, 13, 13] }))).toBe(true);
    expect(freecellIsWon(state({ foundations: [13, 13, 12, 13] }))).toBe(false);
  });
});

describe('FreeCell legal moves + replay', () => {
  it('offers at least one move from a fresh deal', () => {
    const s = freecellNewGame(1);
    expect(freecellGetLegalMoves(s).length).toBeGreaterThan(0);
  });

  it('replay stops at the first illegal move', () => {
    const result = freecellTryApplyMove(freecellNewGame(5), {
      type: 'TableauToFreeCell',
      source: 0,
      destination: 0,
    });
    expect(result.ok).toBe(true);
    // Free-celling into the now-occupied cell 0 again is illegal.
    const again = freecellTryApplyMove(result.next, {
      type: 'TableauToFreeCell',
      source: 1,
      destination: 0,
    });
    expect(again.ok).toBe(false);
  });
});
